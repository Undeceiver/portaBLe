using Microsoft.EntityFrameworkCore;
using Dasync.Collections;
using System.Diagnostics;
using static System.Formats.Asn1.AsnWriter;

namespace portaBLe
{
    public class ScoresRefresh
    {
        public static async Task Refresh(AppContext dbContext)
        {
            dbContext.ChangeTracker.AutoDetectChangesEnabled = false;

            Console.WriteLine("Starting ScoreRefresh ...");
            Stopwatch watch = Stopwatch.StartNew();
            var allLeaderboards = dbContext.Leaderboards
                .Select(lb => new {
                    lb.AccRating,
                    lb.PassRating,
                    lb.TechRating,
                    lb.ModifiersRating,
                    Scores = lb.Scores.Select(s => new {s.Id, s.LeaderboardId, s.Accuracy, s.Modifiers })
                }).ToAsyncEnumerable();
            Console.WriteLine($"Downlaoded Leaderboards at {watch.Elapsed}");
            var newTotalScores = new List<Score>();
            var newScores = new List<Score>();
            await foreach (var leaderboard in allLeaderboards)
            {
                foreach (var s in leaderboard.Scores)
                {
                    (float pp, float bonuspp, float passPP, float accPP, float techPP) = ReplayUtils.PpFromScore(
                        s.Accuracy,
                        s.Modifiers,
                        leaderboard.ModifiersRating,
                        leaderboard.AccRating,
                        leaderboard.PassRating,
                        leaderboard.TechRating);

                    if (float.IsNaN(pp))
                    {
                        pp = 0.0f;
                    }

                    newScores.Add(new() { 
                        Id = s.Id,
                        Pp = pp,
                        BonusPp = bonuspp,
                        PassPP = passPP,
                        AccPP = accPP,
                        TechPP = techPP,
                    });
                }

                foreach ((int i, Score? s) in newScores.OrderByDescending(el => Math.Round(el.Pp, 2)).ThenByDescending(el => Math.Round(el.Accuracy, 4)).Select((value, i) => (i, value)))
                {
                    s.Rank = i + 1;
                }

                newTotalScores.AddRange(newScores);
                newScores.Clear();
            };
            Console.WriteLine($"Processed Leaderboards at {watch.Elapsed}");

            await dbContext.BulkUpdateAsync(newTotalScores, options => options.IgnoreOnUpdateExpression = c => new
            {
                c.PlayerId,
                c.LeaderboardId,
                c.Accuracy,
                c.Modifiers,
            });
            Console.WriteLine($"Saved Leaderboards at {watch.Elapsed}");
            dbContext.ChangeTracker.AutoDetectChangesEnabled = true;
        }
    }
}
