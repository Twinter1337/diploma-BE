using CaoachlyBE.Enums;
using CaoachlyBE.Helpers;
using CaoachlyBE.Models;
using CaoachlyBE.Models.Dtos.Documents;
using CaoachlyBE.Repositories.Interfaces;
using CaoachlyBE.Services;
using CaoachlyBE.Services.Interfaces;
using CaoachlyBE.Tests.Helpers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;

namespace CaoachlyBE.Tests.Documents;

public class DocumentTests
{
    // ── shared identifiers ────────────────────────────────────────────────────
    private readonly Guid _trainerId  = Guid.NewGuid();
    private readonly Guid _documentId = Guid.NewGuid();
    private readonly DateTime _frozenNow = new(2026, 5, 17, 12, 0, 0);

    // ── test-data builders ────────────────────────────────────────────────────
    private UserModel TrainerUser() => new()
    {
        Id           = _trainerId,
        FirstName    = "Anna",
        LastName     = "Koval",
        Email        = "trainer@test.com",
        PasswordHash = "x",
        Role         = UserRole.Trainer,
        IsActive     = true,
        CreatedAt    = _frozenNow,
        UpdatedAt    = _frozenNow,
    };

    private TrainerDocumentModel ExistingDocument() => new()
    {
        Id            = _documentId,
        TrainerId     = _trainerId,
        FileUrl       = "https://blob.example.com/trainer-documents/doc.pdf",
        FileName      = "certificate.pdf",
        FileSizeBytes = 1024,
        DocumentType  = DocumentType.Certificate,
        Status        = DocumentStatus.Pending,
        UploadedAt    = _frozenNow,
    };

    private Mock<IFormFile> PdfFile(long length = 1024) =>
        FakeFile("certificate.pdf", "application/pdf", length);

    private static Mock<IFormFile> FakeFile(string name, string contentType, long length = 1024)
    {
        var mock = new Mock<IFormFile>();
        mock.Setup(f => f.FileName).Returns(name);
        mock.Setup(f => f.ContentType).Returns(contentType);
        mock.Setup(f => f.Length).Returns(length);
        mock.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[length > 0 ? (int)length : 1]));
        return mock;
    }

    private UploadDocumentRequestDto ValidDto(Mock<IFormFile>? file = null) => new()
    {
        File         = (file ?? PdfFile()).Object,
        DocumentType = DocumentType.Certificate,
    };

    // ── service factory ───────────────────────────────────────────────────────
    private TrainerService CreateSut(
        Mock<IUserRepository>?            userRepo     = null,
        Mock<ITrainerDocumentRepository>? docRepo      = null,
        Mock<ISupportTicketRepository>?   ticketRepo   = null,
        Mock<IBlobStorageService>?        blobService  = null,
        Mock<IUnitOfWork>?                unitOfWork   = null,
        Mock<ITimeProvider>?              timeProvider = null)
    {
        userRepo     ??= TestMocks.UserRepo();
        docRepo      ??= TestMocks.TrainerDocumentRepo();
        ticketRepo   ??= TestMocks.SupportTicketRepo();
        unitOfWork   ??= TestMocks.UnitOfWork();
        timeProvider ??= TestMocks.TimeAt(_frozenNow);

        if (blobService is null)
        {
            blobService = TestMocks.BlobStorageService();
            blobService.Setup(b => b.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(),
                                                  It.IsAny<string>(), It.IsAny<string>()))
                       .ReturnsAsync("https://blob.example.com/trainer-documents/doc.pdf");
            blobService.Setup(b => b.GetReadSasUrl(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>()))
                       .Returns("https://blob.example.com/sas-url");
        }

        return new TrainerService(
            userRepo.Object,
            TestMocks.TrainerInfoRepo().Object,
            TestMocks.ScheduleSlotRepo().Object,
            TestMocks.BookingRepository().Object,
            TestMocks.PaymentRepository().Object,
            docRepo.Object,
            ticketRepo.Object,
            blobService.Object,
            TestMocks.EmailService().Object,
            unitOfWork.Object,
            timeProvider.Object);
    }

    // ── UploadDocumentAsync guard tests ───────────────────────────────────────

    [Fact]
    public async Task UploadDocumentAsync_TrainerNotFound_ThrowsKeyNotFoundException()
    {
        var userRepo = TestMocks.UserRepo();
        userRepo.Setup(r => r.GetByIdAsync(_trainerId)).ReturnsAsync((UserModel?)null);

        var sut = CreateSut(userRepo: userRepo);

        await sut.Invoking(s => s.UploadDocumentAsync(_trainerId, _trainerId, ValidDto()))
                 .Should().ThrowAsync<KeyNotFoundException>()
                 .WithMessage("*Trainer not found*");
    }

    [Fact]
    public async Task UploadDocumentAsync_UserNotTrainerRole_ThrowsKeyNotFoundException()
    {
        var userRepo = TestMocks.UserRepo();
        var client   = TrainerUser();
        client.Role  = UserRole.Client;
        userRepo.Setup(r => r.GetByIdAsync(_trainerId)).ReturnsAsync(client);

        var sut = CreateSut(userRepo: userRepo);

        await sut.Invoking(s => s.UploadDocumentAsync(_trainerId, _trainerId, ValidDto()))
                 .Should().ThrowAsync<KeyNotFoundException>()
                 .WithMessage("*Trainer not found*");
    }

    [Fact]
    public async Task UploadDocumentAsync_RequestingUserIsNotOwner_ThrowsUnauthorizedAccessException()
    {
        var userRepo = TestMocks.UserRepo();
        userRepo.Setup(r => r.GetByIdAsync(_trainerId)).ReturnsAsync(TrainerUser());

        var sut = CreateSut(userRepo: userRepo);

        await sut.Invoking(s => s.UploadDocumentAsync(_trainerId, Guid.NewGuid(), ValidDto()))
                 .Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task UploadDocumentAsync_FileTooLarge_ThrowsInvalidOperationException()
    {
        var userRepo = TestMocks.UserRepo();
        userRepo.Setup(r => r.GetByIdAsync(_trainerId)).ReturnsAsync(TrainerUser());

        var hugeFile = PdfFile(length: 10_485_761); // 1 byte over 10 MB

        var sut = CreateSut(userRepo: userRepo);

        await sut.Invoking(s => s.UploadDocumentAsync(_trainerId, _trainerId, ValidDto(hugeFile)))
                 .Should().ThrowAsync<InvalidOperationException>()
                 .WithMessage("*10 MB*");
    }

    [Fact]
    public async Task UploadDocumentAsync_DisallowedExtension_ThrowsInvalidOperationException()
    {
        var userRepo = TestMocks.UserRepo();
        userRepo.Setup(r => r.GetByIdAsync(_trainerId)).ReturnsAsync(TrainerUser());

        var exeFile = FakeFile("malware.exe", "application/pdf"); // bad extension

        var sut = CreateSut(userRepo: userRepo);

        await sut.Invoking(s => s.UploadDocumentAsync(_trainerId, _trainerId, ValidDto(exeFile)))
                 .Should().ThrowAsync<InvalidOperationException>()
                 .WithMessage("*JPG, PNG, and PDF*");
    }

    [Fact]
    public async Task UploadDocumentAsync_DisallowedMimeType_ThrowsInvalidOperationException()
    {
        var userRepo = TestMocks.UserRepo();
        userRepo.Setup(r => r.GetByIdAsync(_trainerId)).ReturnsAsync(TrainerUser());

        var badMime = FakeFile("document.pdf", "application/x-msdownload"); // bad MIME

        var sut = CreateSut(userRepo: userRepo);

        await sut.Invoking(s => s.UploadDocumentAsync(_trainerId, _trainerId, ValidDto(badMime)))
                 .Should().ThrowAsync<InvalidOperationException>()
                 .WithMessage("*JPG, PNG, and PDF*");
    }

    // ── UploadDocumentAsync happy-path tests ──────────────────────────────────

    [Fact]
    public async Task UploadDocumentAsync_HappyPath_AllRepositoriesAndUowCalled()
    {
        var userRepo   = TestMocks.UserRepo();
        var docRepo    = TestMocks.TrainerDocumentRepo();
        var ticketRepo = TestMocks.SupportTicketRepo();
        var uow        = TestMocks.UnitOfWork();
        var blob       = TestMocks.BlobStorageService();

        userRepo.Setup(r => r.GetByIdAsync(_trainerId)).ReturnsAsync(TrainerUser());
        blob.Setup(b => b.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("https://blob.example.com/trainer-documents/doc.pdf");
        blob.Setup(b => b.GetReadSasUrl(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>()))
            .Returns("https://blob.example.com/sas-url");

        var sut = CreateSut(userRepo: userRepo, docRepo: docRepo, ticketRepo: ticketRepo,
                            blobService: blob, unitOfWork: uow);

        await sut.UploadDocumentAsync(_trainerId, _trainerId, ValidDto());

        blob.Verify(b => b.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(),
                                       It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        docRepo.Verify(r => r.AddAsync(It.IsAny<TrainerDocumentModel>()), Times.Once);
        ticketRepo.Verify(r => r.AddAsync(It.IsAny<SupportTicketModel>()), Times.Once);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UploadDocumentAsync_HappyPath_DocumentModelHasCorrectFields()
    {
        var userRepo = TestMocks.UserRepo();
        var docRepo  = TestMocks.TrainerDocumentRepo();
        var blob     = TestMocks.BlobStorageService();

        userRepo.Setup(r => r.GetByIdAsync(_trainerId)).ReturnsAsync(TrainerUser());
        blob.Setup(b => b.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("https://blob.example.com/trainer-documents/doc.pdf");
        blob.Setup(b => b.GetReadSasUrl(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>()))
            .Returns("https://blob.example.com/sas-url");

        TrainerDocumentModel? capturedDoc = null;
        docRepo.Setup(r => r.AddAsync(It.IsAny<TrainerDocumentModel>()))
               .Callback<TrainerDocumentModel>(d => capturedDoc = d);

        var sut = CreateSut(userRepo: userRepo, docRepo: docRepo, blobService: blob);

        await sut.UploadDocumentAsync(_trainerId, _trainerId, ValidDto());

        capturedDoc!.TrainerId.Should().Be(_trainerId);
        capturedDoc.DocumentType.Should().Be(DocumentType.Certificate);
        capturedDoc.Status.Should().Be(DocumentStatus.Pending);
        capturedDoc.UploadedAt.Should().Be(_frozenNow);
        capturedDoc.FileName.Should().Be("certificate.pdf");
    }

    [Fact]
    public async Task UploadDocumentAsync_HappyPath_SupportTicketLinkedToDocument()
    {
        var userRepo   = TestMocks.UserRepo();
        var docRepo    = TestMocks.TrainerDocumentRepo();
        var ticketRepo = TestMocks.SupportTicketRepo();
        var blob       = TestMocks.BlobStorageService();

        userRepo.Setup(r => r.GetByIdAsync(_trainerId)).ReturnsAsync(TrainerUser());
        blob.Setup(b => b.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("https://blob.example.com/trainer-documents/doc.pdf");
        blob.Setup(b => b.GetReadSasUrl(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>()))
            .Returns("https://blob.example.com/sas-url");

        TrainerDocumentModel? capturedDoc    = null;
        SupportTicketModel?   capturedTicket = null;
        docRepo.Setup(r => r.AddAsync(It.IsAny<TrainerDocumentModel>()))
               .Callback<TrainerDocumentModel>(d => capturedDoc = d);
        ticketRepo.Setup(r => r.AddAsync(It.IsAny<SupportTicketModel>()))
                  .Callback<SupportTicketModel>(t => capturedTicket = t);

        var sut = CreateSut(userRepo: userRepo, docRepo: docRepo, ticketRepo: ticketRepo, blobService: blob);

        await sut.UploadDocumentAsync(_trainerId, _trainerId, ValidDto());

        capturedTicket!.RelatedDocumentId.Should().Be(capturedDoc!.Id);
        capturedTicket.Status.Should().Be(TicketStatus.Open);
        capturedTicket.Subject.Should().Be("Документ на перевірку");
        capturedTicket.CreatedBy.Should().Be(_trainerId);
    }

    [Fact]
    public async Task UploadDocumentAsync_HappyPath_ResponseDtoFieldsCorrect()
    {
        var userRepo = TestMocks.UserRepo();
        var blob     = TestMocks.BlobStorageService();

        userRepo.Setup(r => r.GetByIdAsync(_trainerId)).ReturnsAsync(TrainerUser());
        blob.Setup(b => b.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("https://blob.example.com/trainer-documents/doc.pdf");
        blob.Setup(b => b.GetReadSasUrl(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>()))
            .Returns("https://blob.example.com/sas-url");

        var sut = CreateSut(userRepo: userRepo, blobService: blob);

        var result = await sut.UploadDocumentAsync(_trainerId, _trainerId, ValidDto());

        result.Id.Should().NotBeEmpty();
        result.FileName.Should().Be("certificate.pdf");
        result.DocumentType.Should().Be(DocumentType.Certificate);
        result.FileUrl.Should().Be("https://blob.example.com/sas-url");
    }

    // ── DeleteDocumentAsync guard tests ───────────────────────────────────────

    [Fact]
    public async Task DeleteDocumentAsync_TrainerNotFound_ThrowsKeyNotFoundException()
    {
        var userRepo = TestMocks.UserRepo();
        userRepo.Setup(r => r.GetByIdAsync(_trainerId)).ReturnsAsync((UserModel?)null);

        var sut = CreateSut(userRepo: userRepo);

        await sut.Invoking(s => s.DeleteDocumentAsync(_trainerId, _documentId, _trainerId))
                 .Should().ThrowAsync<KeyNotFoundException>()
                 .WithMessage("*Trainer not found*");
    }

    [Fact]
    public async Task DeleteDocumentAsync_UserNotTrainerRole_ThrowsKeyNotFoundException()
    {
        var userRepo = TestMocks.UserRepo();
        var client   = TrainerUser();
        client.Role  = UserRole.Client;
        userRepo.Setup(r => r.GetByIdAsync(_trainerId)).ReturnsAsync(client);

        var sut = CreateSut(userRepo: userRepo);

        await sut.Invoking(s => s.DeleteDocumentAsync(_trainerId, _documentId, _trainerId))
                 .Should().ThrowAsync<KeyNotFoundException>()
                 .WithMessage("*Trainer not found*");
    }

    [Fact]
    public async Task DeleteDocumentAsync_RequestingUserIsNotOwner_ThrowsUnauthorizedAccessException()
    {
        var userRepo = TestMocks.UserRepo();
        userRepo.Setup(r => r.GetByIdAsync(_trainerId)).ReturnsAsync(TrainerUser());

        var sut = CreateSut(userRepo: userRepo);

        await sut.Invoking(s => s.DeleteDocumentAsync(_trainerId, _documentId, Guid.NewGuid()))
                 .Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task DeleteDocumentAsync_DocumentNotFound_ThrowsKeyNotFoundException()
    {
        var userRepo = TestMocks.UserRepo();
        var docRepo  = TestMocks.TrainerDocumentRepo();
        userRepo.Setup(r => r.GetByIdAsync(_trainerId)).ReturnsAsync(TrainerUser());
        docRepo.Setup(r => r.GetByIdAsync(_documentId)).ReturnsAsync((TrainerDocumentModel?)null);

        var sut = CreateSut(userRepo: userRepo, docRepo: docRepo);

        await sut.Invoking(s => s.DeleteDocumentAsync(_trainerId, _documentId, _trainerId))
                 .Should().ThrowAsync<KeyNotFoundException>()
                 .WithMessage("*Document not found*");
    }

    [Fact]
    public async Task DeleteDocumentAsync_DocumentBelongsToDifferentTrainer_ThrowsUnauthorizedAccessException()
    {
        var userRepo = TestMocks.UserRepo();
        var docRepo  = TestMocks.TrainerDocumentRepo();
        userRepo.Setup(r => r.GetByIdAsync(_trainerId)).ReturnsAsync(TrainerUser());

        var otherDoc = ExistingDocument();
        otherDoc.TrainerId = Guid.NewGuid();
        docRepo.Setup(r => r.GetByIdAsync(_documentId)).ReturnsAsync(otherDoc);

        var sut = CreateSut(userRepo: userRepo, docRepo: docRepo);

        await sut.Invoking(s => s.DeleteDocumentAsync(_trainerId, _documentId, _trainerId))
                 .Should().ThrowAsync<UnauthorizedAccessException>();
    }

    // ── DeleteDocumentAsync happy-path tests ──────────────────────────────────

    [Fact]
    public async Task DeleteDocumentAsync_HappyPath_BlobTicketDocDeletedAndUowCalled()
    {
        var userRepo   = TestMocks.UserRepo();
        var docRepo    = TestMocks.TrainerDocumentRepo();
        var ticketRepo = TestMocks.SupportTicketRepo();
        var blob       = TestMocks.BlobStorageService();
        var uow        = TestMocks.UnitOfWork();

        userRepo.Setup(r => r.GetByIdAsync(_trainerId)).ReturnsAsync(TrainerUser());
        docRepo.Setup(r => r.GetByIdAsync(_documentId)).ReturnsAsync(ExistingDocument());

        var sut = CreateSut(userRepo: userRepo, docRepo: docRepo, ticketRepo: ticketRepo,
                            blobService: blob, unitOfWork: uow);

        await sut.DeleteDocumentAsync(_trainerId, _documentId, _trainerId);

        ticketRepo.Verify(r => r.DetachAndCloseByDocumentAsync(_documentId, It.IsAny<DateTime>()), Times.Once);
        blob.Verify(b => b.DeleteAsync(ExistingDocument().FileUrl, It.IsAny<string>()), Times.Once);
        docRepo.Verify(r => r.DeleteAsync(_documentId), Times.Once);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
