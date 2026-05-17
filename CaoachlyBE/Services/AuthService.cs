using System.Security.Cryptography;
using System.Text;
using AutoMapper;
using CaoachlyBE.Enums;
using CaoachlyBE.Helpers;
using CaoachlyBE.Models;
using CaoachlyBE.Models.Dtos.Auth;
using CaoachlyBE.Models.Dtos.Users;
using CaoachlyBE.Repositories.Interfaces;
using CaoachlyBE.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace CaoachlyBE.Services;

public class AuthService(
    IUserRepository userRepo,
    IClientInfoRepository clientInfoRepo,
    ITrainerInfoRepository trainerInfoRepo,
    IRefreshTokenRepository refreshTokenRepo,
    IPasswordResetTokenRepository passwordResetTokenRepo,
    IUnitOfWork unitOfWork,
    JwtHelper jwtHelper,
    IEmailService emailService,
    IOptions<EmailSettings> emailSettings,
    IMapper mapper) : IAuthService
{
    public async Task<AuthResponseDto> RegisterAsync(RegisterDto dto)
    {
        if (dto.Role == UserRole.Admin)
            throw new InvalidOperationException("Admin accounts cannot be self-registered.");

        if (await userRepo.EmailExistsAsync(dto.Email))
            throw new InvalidOperationException("Email is already registered.");

        var now = DateTime.UtcNow;
        var userId = Guid.NewGuid();

        var user = new UserModel
        {
            Id = userId,
            Email = dto.Email.ToLowerInvariant(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Role = dto.Role,
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        await userRepo.AddAsync(user);

        if (dto.Role == UserRole.Client)
        {
            await clientInfoRepo.AddAsync(new ClientInfoModel
            {
                Id = Guid.NewGuid(),
                UserId = userId
            });
        }
        else
        {
            await trainerInfoRepo.AddAsync(new TrainerInfoModel
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                VerificationStatus = VerificationStatus.NotVerified
            });
        }

        var (rawToken, hashedToken) = jwtHelper.GenerateRefreshToken();

        await refreshTokenRepo.AddAsync(new RefreshTokenModel
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Token = hashedToken,
            ExpiresAt = now.AddDays(30),
            CreatedAt = now
        });

        await unitOfWork.SaveChangesAsync();

        return new AuthResponseDto
        {
            AccessToken = jwtHelper.GenerateAccessToken(user),
            RefreshToken = rawToken,
            User = mapper.Map<UserDto>(user)
        };
    }

    public async Task<AuthResponseDto> LoginAsync(LoginDto dto)
    {
        var user = await userRepo.GetByEmailAsync(dto.Email.ToLowerInvariant())
            ?? throw new UnauthorizedAccessException("Invalid credentials.");

        if (!BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid credentials.");

        var now = DateTime.UtcNow;
        var (rawToken, hashedToken) = jwtHelper.GenerateRefreshToken();

        await refreshTokenRepo.AddAsync(new RefreshTokenModel
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = hashedToken,
            ExpiresAt = now.AddDays(30),
            CreatedAt = now
        });

        await unitOfWork.SaveChangesAsync();

        return new AuthResponseDto
        {
            AccessToken = jwtHelper.GenerateAccessToken(user),
            RefreshToken = rawToken,
            User = mapper.Map<UserDto>(user)
        };
    }

    public async Task<AuthResponseDto> RefreshAsync(RefreshTokenRequestDto dto)
    {
        var hashedToken = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(dto.RefreshToken))).ToLowerInvariant();

        var stored = await refreshTokenRepo.GetByHashedTokenAsync(hashedToken)
            ?? throw new UnauthorizedAccessException("Invalid refresh token.");

        if (stored.ExpiresAt <= DateTime.UtcNow)
            throw new UnauthorizedAccessException("Refresh token has expired.");

        var user = await userRepo.GetByIdAsync(stored.UserId)
            ?? throw new UnauthorizedAccessException("User not found.");

        if (!user.IsActive)
            throw new UnauthorizedAccessException("Account is inactive.");

        var now = DateTime.UtcNow;
        var (rawToken, newHashedToken) = jwtHelper.GenerateRefreshToken();

        await refreshTokenRepo.DeleteAsync(stored);
        await refreshTokenRepo.AddAsync(new RefreshTokenModel
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = newHashedToken,
            ExpiresAt = now.AddDays(30),
            CreatedAt = now
        });

        await unitOfWork.SaveChangesAsync();

        return new AuthResponseDto
        {
            AccessToken = jwtHelper.GenerateAccessToken(user),
            RefreshToken = rawToken,
            User = mapper.Map<UserDto>(user)
        };
    }

    public async Task ForgotPasswordAsync(ForgotPasswordDto dto)
    {
        var user = await userRepo.GetByEmailAsync(dto.Email.ToLowerInvariant());
        if (user is null || !user.IsActive) return;

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var now = DateTime.UtcNow;

        await passwordResetTokenRepo.AddAsync(new PasswordResetTokenModel
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = token,
            ExpiresAt = now.AddHours(48),
            CreatedAt = now
        });
        await unitOfWork.SaveChangesAsync();

        var resetUrl = $"{emailSettings.Value.FrontendBaseUrl.TrimEnd('/')}/reset-password?token={token}";
        await emailService.SendPasswordResetAsync(user.Email, resetUrl, user.FirstName);
    }

    public async Task ResetPasswordAsync(ResetPasswordDto dto)
    {
        var stored = await passwordResetTokenRepo.GetByTokenAsync(dto.Token)
            ?? throw new InvalidOperationException("Invalid or expired token");

        var now = DateTime.UtcNow;
        if (stored.UsedAt is not null || stored.ExpiresAt <= now)
            throw new InvalidOperationException("Invalid or expired token");

        var newHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
        await userRepo.UpdatePasswordHashAsync(stored.UserId, newHash, now);
        await passwordResetTokenRepo.MarkUsedAsync(stored.Id, now);
        await unitOfWork.SaveChangesAsync();
    }
}
