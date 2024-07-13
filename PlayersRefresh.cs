using Dasync.Collections;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace portaBLe
{
    public class PlayersRefresh
    {
        private static async Task<(List<Score>, List<Player>)> CalculateBatch(AppContext dbContext, IAsyncEnumerable<IGrouping<string, ScoreSelection>> groups, Dictionary<int, float> weights)
        {
            dbContext.ChangeTracker.AutoDetectChangesEnabled = false;
            var scoreUpdates = new List<Score>();
            var playerUpdates = new List<Player>();
            await foreach (var group in groups)
            {
                try {
                    Player player = new() { Id = group.Key };
                    playerUpdates.Add(player);


                    float resultPP = 0f;
                    float accPP = 0f;
                    float techPP = 0f;
                    float passPP = 0f;

                    foreach ((int i, var s) in group.OrderByDescending(s => s.Pp).Select((value, i) => (i, value)))
                    {
                        float weight = weights[i];
                        if (s.Weight != weight)
                        {
                            scoreUpdates.Add(new() { Id = s.Id, Weight = weight });
                        }
                        resultPP += s.Pp * weight;
                        accPP += s.AccPP * weight;
                        techPP += s.TechPP * weight;
                        passPP += s.PassPP * weight;
                    }
                    player.Pp = resultPP;

                    player.AccPp = accPP;
                    player.TechPp = techPP;
                    player.PassPp = passPP;
                } catch (Exception) {
                }
            }

            return (scoreUpdates, playerUpdates);
        }

        public static async Task Refresh(AppContext dbContext) {
            dbContext.ChangeTracker.AutoDetectChangesEnabled = false;
            
            Console.WriteLine("Starting PlayerRefresh ...");
            Stopwatch watch = Stopwatch.StartNew();
            var weights = new Dictionary<int, float>();
            for (int i = 0; i < 10000; i++)
            {
                weights[i] = MathF.Pow(0.965f, i);
            }

            Console.WriteLine($"Calculated Weights after {watch.Elapsed}");

            var scores = dbContext
                .Scores
                .Select(s => new ScoreSelection { 
                    Id = s.Id, 
                    Accuracy = s.Accuracy, 
                    Rank = s.Rank, 
                    Pp = s.Pp, 
                    AccPP = s.AccPP, 
                    TechPP = s.TechPP, 
                    PassPP = s.PassPP, 
                    Weight = s.Weight, 
                    PlayerId = s.PlayerId, 
                    Country = s.Player.Country 
                })
                .GroupBy(s => s.PlayerId)
                .ToAsyncEnumerable();
            Console.WriteLine($"Downloaded Scores after {watch.Elapsed}");

            (List<Score> scoreUpdates, List<Player> playerUpdates) = await CalculateBatch(dbContext, scores, weights);
            Console.WriteLine($"Calculated Batches after {watch.Elapsed}");

            Dictionary<string, int> countries = new();
            foreach ((int i, Player p) in playerUpdates.OrderByDescending(p => p.Pp).Select((value, i) => (i, value)))
            {
                p.Rank = i + 1;
                if (p.Country != null) {
                    if (!countries.TryGetValue(p.Country, out int value))
                    {
                        countries[p.Country] = value = 1;
                    }

                    p.CountryRank = value;
                    countries[p.Country] = ++value;
                }
            }
            Console.WriteLine($"Refreshed Countryrank after {watch.Elapsed}");
            await dbContext.BulkUpdateAsync(scoreUpdates, options => options.IgnoreOnUpdateExpression = c => new
            {
                c.Pp,
                c.AccPP,
                c.TechPP,
                c.PassPP,
                c.BonusPp,
                c.PlayerId,
                c.Rank,
                c.LeaderboardId,
                c.Accuracy,
                c.Modifiers
            });
            await dbContext.BulkUpdateAsync(playerUpdates, options => options.IgnoreOnUpdateExpression = c => new
            {
                c.Name,
                c.Country,
                c.Avatar
            });
            Console.WriteLine($"Saved Changes after {watch.Elapsed}");
        }
    }

    public class ScoreSelection {
        public int Id { get; set; } 
        public float Accuracy { get; set; } 
        public int Rank { get; set; } 
        public float Pp { get; set; } 
        public float AccPP { get; set; }  
        public float TechPP { get; set; } 
        public float PassPP { get; set; } 
        public float Weight { get; set; } 
        public string PlayerId { get; set; } 
        public string Country { get; set; } 
    }
}
