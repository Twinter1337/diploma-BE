using CaoachlyBE.Enums;
using CaoachlyBE.Helpers;
using CaoachlyBE.Models;
using CaoachlyBE.Models.Dtos.Trainers;
using CaoachlyBE.Repositories.Interfaces;
using CaoachlyBE.Services;
using CaoachlyBE.Tests.Helpers;
using FluentAssertions;
using Moq;

namespace CaoachlyBE.Tests.Trainers;

public class TrainerSearchTests
{
    private readonly DateTime _frozenNow = new(2026, 5, 17, 12, 0, 0);

    // ── test-data builders ────────────────────────────────────────────────────
    private static TrainerSummaryModel Trainer(
        string firstName, decimal rating, decimal? price = null) => new()
    {
        Id                 = Guid.NewGuid(),
        FirstName          = firstName,
        LastName           = "Test",
        Rating             = rating,
        MinSlotPrice       = price,
        VerificationStatus = VerificationStatus.Verified,
        SpecializationTags = [],
        DisabilityTags     = [],
        MethodologyTags    = [],
    };

    private static TrainerSearchFilterDto EmptyFilter() => new();

    // ── service factory ───────────────────────────────────────────────────────
    private TrainerService CreateSut(Mock<IUserRepository>? userRepo = null)
    {
        userRepo ??= TestMocks.UserRepo();

        return new TrainerService(
            userRepo.Object,
            TestMocks.TrainerInfoRepo().Object,
            TestMocks.ScheduleSlotRepo().Object,
            TestMocks.BookingRepository().Object,
            TestMocks.PaymentRepository().Object,
            TestMocks.TrainerDocumentRepo().Object,
            TestMocks.SupportTicketRepo().Object,
            TestMocks.BlobStorageService().Object,
            TestMocks.EmailService().Object,
            TestMocks.UnitOfWork().Object,
            TestMocks.TimeAt(_frozenNow).Object);
    }

    // ── empty / basic results ─────────────────────────────────────────────────

    [Fact]
    public async Task SearchAsync_NoResults_ReturnsEmptyPagedResult()
    {
        var userRepo = TestMocks.UserRepo();
        userRepo.Setup(r => r.SearchTrainersAsync(It.IsAny<TrainerSearchFilter>()))
                .ReturnsAsync([]);

        var sut = CreateSut(userRepo);

        var result = await sut.SearchAsync(EmptyFilter(), page: 1, pageSize: 10,
                                           sortBy: "rating", sortOrder: "desc");

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
        result.TotalPages.Should().Be(0);
    }

    [Fact]
    public async Task SearchAsync_ReturnsCorrectTotalCountAndTotalPages()
    {
        var userRepo = TestMocks.UserRepo();
        var trainers = Enumerable.Range(1, 5)
            .Select(i => Trainer($"Trainer{i}", rating: i))
            .ToList();
        userRepo.Setup(r => r.SearchTrainersAsync(It.IsAny<TrainerSearchFilter>()))
                .ReturnsAsync(trainers);

        var sut = CreateSut(userRepo);

        var result = await sut.SearchAsync(EmptyFilter(), page: 1, pageSize: 2,
                                           sortBy: "rating", sortOrder: "desc");

        result.TotalCount.Should().Be(5);
        result.TotalPages.Should().Be(3); // ceil(5/2) = 3
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(2);
    }

    // ── pagination tests ──────────────────────────────────────────────────────

    [Fact]
    public async Task SearchAsync_Page1_ReturnsFirstPageItems()
    {
        var userRepo = TestMocks.UserRepo();
        // All same rating so sort order is stable — easier to reason about.
        var trainers = Enumerable.Range(1, 5)
            .Select(i => Trainer($"Trainer{i}", rating: 4m))
            .ToList();
        userRepo.Setup(r => r.SearchTrainersAsync(It.IsAny<TrainerSearchFilter>()))
                .ReturnsAsync(trainers);

        var sut = CreateSut(userRepo);

        var result = await sut.SearchAsync(EmptyFilter(), page: 1, pageSize: 2,
                                           sortBy: "rating", sortOrder: "desc");

        result.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task SearchAsync_Page3Of5ItemsPageSize2_ReturnsLastItem()
    {
        var userRepo = TestMocks.UserRepo();
        var trainers = Enumerable.Range(1, 5)
            .Select(i => Trainer($"Trainer{i}", rating: 4m))
            .ToList();
        userRepo.Setup(r => r.SearchTrainersAsync(It.IsAny<TrainerSearchFilter>()))
                .ReturnsAsync(trainers);

        var sut = CreateSut(userRepo);

        var result = await sut.SearchAsync(EmptyFilter(), page: 3, pageSize: 2,
                                           sortBy: "rating", sortOrder: "desc");

        result.Items.Should().HaveCount(1);
    }

    // ── sorting tests ─────────────────────────────────────────────────────────

    [Fact]
    public async Task SearchAsync_SortByRatingDesc_ReturnsHighestRatingFirst()
    {
        var userRepo = TestMocks.UserRepo();
        userRepo.Setup(r => r.SearchTrainersAsync(It.IsAny<TrainerSearchFilter>()))
                .ReturnsAsync([
                    Trainer("Low",  rating: 2.0m),
                    Trainer("High", rating: 4.9m),
                    Trainer("Mid",  rating: 3.5m),
                ]);

        var sut = CreateSut(userRepo);

        var result = await sut.SearchAsync(EmptyFilter(), 1, 10, "rating", "desc");

        result.Items.First().FirstName.Should().Be("High");
        result.Items.Last().FirstName.Should().Be("Low");
    }

    [Fact]
    public async Task SearchAsync_SortByRatingAsc_ReturnsLowestRatingFirst()
    {
        var userRepo = TestMocks.UserRepo();
        userRepo.Setup(r => r.SearchTrainersAsync(It.IsAny<TrainerSearchFilter>()))
                .ReturnsAsync([
                    Trainer("Low",  rating: 2.0m),
                    Trainer("High", rating: 4.9m),
                    Trainer("Mid",  rating: 3.5m),
                ]);

        var sut = CreateSut(userRepo);

        var result = await sut.SearchAsync(EmptyFilter(), 1, 10, "rating", "asc");

        result.Items.First().FirstName.Should().Be("Low");
        result.Items.Last().FirstName.Should().Be("High");
    }

    [Fact]
    public async Task SearchAsync_SortByPriceAsc_ReturnsCheapestFirst()
    {
        var userRepo = TestMocks.UserRepo();
        userRepo.Setup(r => r.SearchTrainersAsync(It.IsAny<TrainerSearchFilter>()))
                .ReturnsAsync([
                    Trainer("Expensive", rating: 4m, price: 800m),
                    Trainer("Cheap",     rating: 4m, price: 200m),
                    Trainer("Mid",       rating: 4m, price: 500m),
                ]);

        var sut = CreateSut(userRepo);

        var result = await sut.SearchAsync(EmptyFilter(), 1, 10, "price", "asc");

        result.Items.First().FirstName.Should().Be("Cheap");
        result.Items.Last().FirstName.Should().Be("Expensive");
    }

    [Fact]
    public async Task SearchAsync_SortByPriceDesc_ReturnsMostExpensiveFirst()
    {
        var userRepo = TestMocks.UserRepo();
        userRepo.Setup(r => r.SearchTrainersAsync(It.IsAny<TrainerSearchFilter>()))
                .ReturnsAsync([
                    Trainer("Expensive", rating: 4m, price: 800m),
                    Trainer("Cheap",     rating: 4m, price: 200m),
                    Trainer("Mid",       rating: 4m, price: 500m),
                ]);

        var sut = CreateSut(userRepo);

        var result = await sut.SearchAsync(EmptyFilter(), 1, 10, "price", "desc");

        result.Items.First().FirstName.Should().Be("Expensive");
        result.Items.Last().FirstName.Should().Be("Cheap");
    }

    [Fact]
    public async Task SearchAsync_UnknownSortBy_DefaultsToRatingDesc()
    {
        var userRepo = TestMocks.UserRepo();
        userRepo.Setup(r => r.SearchTrainersAsync(It.IsAny<TrainerSearchFilter>()))
                .ReturnsAsync([
                    Trainer("Low",  rating: 2.0m),
                    Trainer("High", rating: 4.9m),
                ]);

        var sut = CreateSut(userRepo);

        var result = await sut.SearchAsync(EmptyFilter(), 1, 10, "unknown", "asc");

        result.Items.First().FirstName.Should().Be("High");
    }

    // ── filter mapping test ───────────────────────────────────────────────────

    [Fact]
    public async Task SearchAsync_FilterParams_PassedThroughToRepository()
    {
        var userRepo = TestMocks.UserRepo();
        userRepo.Setup(r => r.SearchTrainersAsync(It.IsAny<TrainerSearchFilter>()))
                .ReturnsAsync([]);

        TrainerSearchFilter? capturedFilter = null;
        userRepo.Setup(r => r.SearchTrainersAsync(It.IsAny<TrainerSearchFilter>()))
                .Callback<TrainerSearchFilter>(f => capturedFilter = f)
                .ReturnsAsync([]);

        var sut = CreateSut(userRepo);

        var filter = new TrainerSearchFilterDto
        {
            City        = "Kyiv",
            MinPrice    = 100m,
            MaxPrice    = 800m,
            MinRating   = 3.5m,
            Name        = "John",
            IsVerified  = true,
        };

        await sut.SearchAsync(filter, 1, 10, "rating", "desc");

        capturedFilter!.City.Should().Be("Kyiv");
        capturedFilter.MinPrice.Should().Be(100m);
        capturedFilter.MaxPrice.Should().Be(800m);
        capturedFilter.MinRating.Should().Be(3.5m);
        capturedFilter.Name.Should().Be("John");
        capturedFilter.IsVerified.Should().BeTrue();
    }

    // ── DTO mapping test ──────────────────────────────────────────────────────

    [Fact]
    public async Task SearchAsync_TrainerFound_ItemDtoFieldsMappedCorrectly()
    {
        var userRepo  = TestMocks.UserRepo();
        var trainerId = Guid.NewGuid();
        var trainer = new TrainerSummaryModel
        {
            Id                 = trainerId,
            FirstName          = "Anna",
            LastName           = "Koval",
            City               = "Lviv",
            AvatarUrl          = "https://blob.example.com/avatar.jpg",
            Rating             = 4.7m,
            ReviewsCount       = 42,
            MinSlotPrice       = 350m,
            IsAccessible       = true,
            VerificationStatus = VerificationStatus.Verified,
            SpecializationTags = [],
            DisabilityTags     = [],
            MethodologyTags    = [],
        };
        userRepo.Setup(r => r.SearchTrainersAsync(It.IsAny<TrainerSearchFilter>()))
                .ReturnsAsync([trainer]);

        var sut = CreateSut(userRepo);

        var result = await sut.SearchAsync(EmptyFilter(), 1, 10, "rating", "desc");

        var item = result.Items.Single();
        item.Id.Should().Be(trainerId);
        item.FirstName.Should().Be("Anna");
        item.LastName.Should().Be("Koval");
        item.City.Should().Be("Lviv");
        item.AvatarUrl.Should().Be("https://blob.example.com/avatar.jpg");
        item.Rating.Should().Be(4.7m);
        item.ReviewsCount.Should().Be(42);
        item.MinPrice.Should().Be(350m);
        item.VerificationStatus.Should().Be(VerificationStatus.Verified);
    }
}
