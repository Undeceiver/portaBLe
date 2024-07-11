using Microsoft.EntityFrameworkCore;
using Dasync.Collections;

namespace portaBLe
{
    public class ScoresRefresh
    {
        public static async Task Refresh(AppContext dbContext) {
            dbContext.ChangeTracker.AutoDetectChangesEnabled = false; 
            var allLeaderboards = await dbContext.Leaderboards
                .Select(lb => new {
                    lb.AccRating,
                    lb.PassRating,
                    lb.TechRating,
                    lb.ModifiersRating,
                    Scores = lb.Scores.Select(s => new { s.Id, s.LeaderboardId, s.Accuracy, s.Modifiers })
                }).ToListAsync();
            await allLeaderboards.ParallelForEachAsync(async leaderboard => {
                var allScores = leaderboard.Scores.ToList();

                var newScores = new List<Score>();

                foreach (var s in allScores)
                {
                    var score = new Score() { Id = s.Id };
                    try {
                        dbContext.Scores.Attach(score);
                    } catch { }
                    (score.Pp, score.BonusPp, score.PassPP, score.AccPP, score.TechPP) = ReplayUtils.PpFromScore(
                        s.Accuracy,
                        s.Modifiers,
                        leaderboard.ModifiersRating,
                        leaderboard.AccRating,
                        leaderboard.PassRating,
                        leaderboard.TechRating);

                    if (float.IsNaN(score.Pp))
                    {
                        score.Pp = 0.0f;
                    }
                        
                    dbContext.Entry(score).Property(x => x.Pp).IsModified = true;
                    dbContext.Entry(score).Property(x => x.PassPP).IsModified = true;
                    dbContext.Entry(score).Property(x => x.AccPP).IsModified = true;
                    dbContext.Entry(score).Property(x => x.TechPP).IsModified = true;
                    dbContext.Entry(score).Property(x => x.BonusPp).IsModified = true;

                    newScores.Add(score);
                }

                var rankedScores = newScores
                        .OrderByDescending(el => Math.Round(el.Pp, 2))
                        .ThenByDescending(el => Math.Round(el.Accuracy, 4))
                        .ToList();
                foreach ((int i, var s) in rankedScores.Select((value, i) => (i, value)))
                {
                    s.Rank = i + 1;
                    dbContext.Entry(s).Property(x => x.Rank).IsModified = true;
                }
            }, maxDegreeOfParallelism: 20);

            await dbContext.BulkSaveChangesAsync();
            dbContext.ChangeTracker.AutoDetectChangesEnabled = true; 
        }
    }
}
