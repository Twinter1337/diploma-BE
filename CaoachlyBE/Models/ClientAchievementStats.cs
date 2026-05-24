namespace CaoachlyBE.Models;

public record ClientAchievementStats(
    int CompletedSessions,
    int DistinctTrainers,
    int DistinctSpecializations,
    int DistinctCities,
    int MaxSessionsWithOneTrainer,
    int MaxSessionsAtOneLocation,
    bool HasEarlyMorningSession,
    bool HasLateEveningSession,
    int MaxSessionsInOneDay
);
