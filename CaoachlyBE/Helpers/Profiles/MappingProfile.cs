using AutoMapper;
using CaoachlyBE.Models;
using CaoachlyBE.Models.Dtos.Achievements;
using CaoachlyBE.Models.Dtos.Bookings;
using CaoachlyBE.Models.Dtos.Clients;
using CaoachlyBE.Models.Dtos.Documents;
using CaoachlyBE.Models.Dtos.Notes;
using CaoachlyBE.Models.Dtos.Notifications;
using CaoachlyBE.Models.Dtos.Payments;
using CaoachlyBE.Models.Dtos.Reviews;
using CaoachlyBE.Models.Dtos.Schedule;
using CaoachlyBE.Models.Dtos.Tags;
using CaoachlyBE.Models.Dtos.Tickets;
using CaoachlyBE.Models.Dtos.Trainers;
using CaoachlyBE.Models.Dtos.Users;

namespace CaoachlyBE.Helpers.Profiles;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<UserModel, UserDto>();
        CreateMap<TrainerInfoModel, TrainerInfoDto>();
        CreateMap<ClientInfoModel, ClientInfoDto>();
        CreateMap<TrainerDocumentModel, TrainerDocumentDto>();
        CreateMap<TagModel, TagDto>();
        CreateMap<TagModel, TagListItemDto>();
        CreateMap<ScheduleSlotModel, ScheduleSlotDto>();
        CreateMap<BookingModel, BookingDto>();
        CreateMap<ClientBookingModel, UserBookingListItemDto>()
            .ForMember(d => d.DurationMinutes,
                       o => o.MapFrom(s => (int)(s.EndTime - s.StartTime).TotalMinutes));
        CreateMap<BookingHistoryItemModel, BookingHistoryItemDto>()
            .ForMember(d => d.BookingStatus, o => o.MapFrom(s => s.Status))
            .ForMember(d => d.Review, o => o.MapFrom(s =>
                s.ReviewRating.HasValue
                    ? new BookingReviewDto { Rating = s.ReviewRating.Value, Comment = s.ReviewComment }
                    : null));
        CreateMap<SessionNoteModel, SessionNoteDto>();
        CreateMap<PaymentModel, PaymentDto>();
        CreateMap<ReviewModel, ReviewDto>();
        CreateMap<TrainerReviewModel, TrainerReviewDto>();
        CreateMap<NotificationModel, NotificationDto>();
        CreateMap<AchievementModel, AchievementDto>();
        CreateMap<UserAchievementModel, UserAchievementDto>();
        CreateMap<UserAchievementListItemModel, UserAchievementListItemDto>();
        CreateMap<SupportTicketModel, SupportTicketDto>();
    }
}
