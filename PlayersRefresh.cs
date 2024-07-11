using Microsoft.EntityFrameworkCore;

namespace portaBLe
{
    public class PlayersRefresh
    {
        private static async Task CalculateBatch(AppContext dbContext, List<IGrouping<string, ScoreSelection>> groups, Dictionary<int, float> weights)
        {
            dbContext.ChangeTracker.AutoDetectChangesEnabled = false;
            foreach (var group in groups)
            {
                try {
                    Player player = new Player { Id = group.Key };
                    try {
                        dbContext.Players.Attach(player);
                    } catch { }

                    float resultPP = 0f;
                    float accPP = 0f;
                    float techPP = 0f;
                    float passPP = 0f;

                    foreach ((int i, var s) in group.OrderByDescending(s => s.Pp).Select((value, i) => (i, value)))
                    {
                        float weight = weights[i];
                        if (s.Weight != weight)
                        {
                            var score = new Score() { Id = s.Id, Weight = weight };
                            try {
                                dbContext.Scores.Attach(score);
                            } catch { }
                            dbContext.Entry(score).Property(x => x.Weight).IsModified = true;
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

                    dbContext.Entry(player).Property(x => x.Pp).IsModified = true;
                    dbContext.Entry(player).Property(x => x.AccPp).IsModified = true;
                    dbContext.Entry(player).Property(x => x.TechPp).IsModified = true;
                    dbContext.Entry(player).Property(x => x.PassPp).IsModified = true;
                } catch (Exception e) {
                }
            }
                
            await dbContext.BulkSaveChangesAsync();
        }

        public static async Task Refresh(AppContext dbContext) {
            dbContext.ChangeTracker.AutoDetectChangesEnabled = false;

            var weights = new Dictionary<int, float>();
            for (int i = 0; i < 10000; i++)
            {
                weights[i] = MathF.Pow(0.965f, i);
            }

            var scores = await dbContext
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
                .ToListAsync();

            var scoreGroups = scores.GroupBy(s => s.PlayerId).ToList();
            for (int i = 0; i < scoreGroups.Count; i += 5000)
            {
                await CalculateBatch(dbContext, scoreGroups.Skip(i).Take(5000).ToList(), weights);
            }

            Dictionary<string, int> countries = new Dictionary<string, int>();
            var ranked = await dbContext
                .Players
                .OrderByDescending(p => p.Pp)
                .ToListAsync();
            foreach ((int i, Player p) in ranked.Select((value, i) => (i, value)))
            {
                p.Rank = i + 1;
                dbContext.Entry(p).Property(x => x.Rank).IsModified = true;
                if (p.Country != null) {
                    if (!countries.ContainsKey(p.Country))
                    {
                        countries[p.Country] = 1;
                    }

                    p.CountryRank = countries[p.Country];
                    dbContext.Entry(p).Property(x => x.CountryRank).IsModified = true;
                    countries[p.Country]++;
                }
            }
            await dbContext.BulkSaveChangesAsync();
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
