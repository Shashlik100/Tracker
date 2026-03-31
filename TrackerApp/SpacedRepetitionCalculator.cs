namespace TrackerApp;

internal static class SpacedRepetitionCalculator
{
    public static ReviewResult ApplySm2(
        int currentRepetitionCount,
        int currentLapses,
        double currentEaseFactor,
        double currentIntervalDays,
        ReviewRating rating,
        DateTime reviewedAt)
    {
        var easeFactor = currentEaseFactor <= 0 ? 2.5 : currentEaseFactor;
        var intervalDays = Math.Max(0, currentIntervalDays);
        var repetitions = Math.Max(0, currentRepetitionCount);
        var lapses = Math.Max(0, currentLapses);

        if (rating == ReviewRating.Again)
        {
            return new ReviewResult
            {
                NextLevel = 1,
                NextRepetitionCount = 0,
                NextLapses = lapses + 1,
                NextEaseFactor = Math.Max(1.3, easeFactor - 0.2),
                NextIntervalDays = 0,
                NextDueDate = reviewedAt.Date,
                IsSuccessful = false
            };
        }

        var quality = rating switch
        {
            ReviewRating.Hard => 3,
            ReviewRating.Good => 4,
            ReviewRating.Easy => 5,
            ReviewRating.Perfect => 5,
            _ => 4
        };

        var efAdjustment = 0.1 - (5 - quality) * (0.08 + (5 - quality) * 0.02);
        if (rating == ReviewRating.Hard)
        {
            efAdjustment -= 0.15;
        }
        else if (rating == ReviewRating.Perfect)
        {
            efAdjustment += 0.15;
        }

        easeFactor = Math.Max(1.3, easeFactor + efAdjustment);
        repetitions += 1;

        if (repetitions == 1)
        {
            intervalDays = rating switch
            {
                ReviewRating.Hard => 1,
                ReviewRating.Good => 1,
                ReviewRating.Easy => 2,
                ReviewRating.Perfect => 3,
                _ => 1
            };
        }
        else if (repetitions == 2)
        {
            intervalDays = rating switch
            {
                ReviewRating.Hard => 3,
                ReviewRating.Good => 6,
                ReviewRating.Easy => 8,
                ReviewRating.Perfect => 10,
                _ => 6
            };
        }
        else
        {
            var multiplier = rating switch
            {
                ReviewRating.Hard => Math.Max(1.2, easeFactor - 0.35),
                ReviewRating.Good => easeFactor,
                ReviewRating.Easy => easeFactor * 1.15,
                ReviewRating.Perfect => easeFactor * 1.35,
                _ => easeFactor
            };

            intervalDays = Math.Max(1, Math.Round(intervalDays * multiplier));
        }

        return new ReviewResult
        {
            NextLevel = repetitions + 1,
            NextRepetitionCount = repetitions,
            NextLapses = lapses,
            NextEaseFactor = easeFactor,
            NextIntervalDays = intervalDays,
            NextDueDate = reviewedAt.Date.AddDays(intervalDays),
            IsSuccessful = true
        };
    }
}
