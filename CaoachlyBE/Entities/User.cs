using System;
using System.Collections.Generic;

namespace CaoachlyBE.Entities;

public partial class User
{
    public Guid Id { get; set; }

    public string Email { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public short Role { get; set; }

    public string FirstName { get; set; } = null!;

    public string LastName { get; set; } = null!;

    public string? AvatarUrl { get; set; }

    public string? City { get; set; }

    public string? Phone { get; set; }

    public DateOnly? BirthDate { get; set; }

    public short? Gender { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<Booking> Bookings { get; set; } = new List<Booking>();

    public virtual ClientInfo? ClientInfo { get; set; }

    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    public virtual ICollection<PasswordResetToken> PasswordResetTokens { get; set; } = new List<PasswordResetToken>();

    public virtual ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();

    public virtual ICollection<Review> ReviewClients { get; set; } = new List<Review>();

    public virtual ICollection<Review> ReviewTrainers { get; set; } = new List<Review>();

    public virtual ICollection<ScheduleSlot> ScheduleSlots { get; set; } = new List<ScheduleSlot>();

    public virtual ICollection<SessionNote> SessionNotes { get; set; } = new List<SessionNote>();

    public virtual ICollection<SupportTicket> SupportTicketAssignedToNavigations { get; set; } = new List<SupportTicket>();

    public virtual ICollection<SupportTicket> SupportTicketCreatedByNavigations { get; set; } = new List<SupportTicket>();

    public virtual ICollection<TrainerDocument> TrainerDocumentReviewedByNavigations { get; set; } = new List<TrainerDocument>();

    public virtual ICollection<TrainerDocument> TrainerDocumentTrainers { get; set; } = new List<TrainerDocument>();

    public virtual TrainerInfo? TrainerInfo { get; set; }

    public virtual ICollection<UserAchievement> UserAchievements { get; set; } = new List<UserAchievement>();

    public virtual ICollection<Tag> Tags { get; set; } = new List<Tag>();
}
