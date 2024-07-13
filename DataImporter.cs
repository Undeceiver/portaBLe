using System;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Collections.Concurrent;
using System.Diagnostics;
using Z.EntityFramework.Extensions;

namespace portaBLe
{
    public static class DataImporter
    {
        public static void ImportJsonData(RootObject rootObject, AppContext dbContext)
        {
            Stopwatch watch = Stopwatch.StartNew();
            dbContext.ChangeTracker.AutoDetectChangesEnabled = false;
            
            // Disable WAL
            dbContext.Database.ExecuteSql($"PRAGMA journal_mode=OFF;");
            dbContext.Database.ExecuteSql($"PRAGMA synchronous=OFF;");

            foreach (var item in rootObject.Maps)
            {
                if (item.ModifiersRating != null) {
                    item.ModifiersRating.Id = 0;
                }
            }

            Console.WriteLine($"Start Import at {watch.Elapsed}");

            var leaderboards = rootObject.Maps.Select(map => new Leaderboard
            {
                Id = map.Id,
                Name = map.Name,
                Hash = map.Hash,
                SongId = map.SongId,
                ModeName = map.ModeName,
                DifficultyName = map.DifficultyName,
                PassRating = map.PassRating,
                AccRating = map.AccRating,
                TechRating = map.TechRating,
                PredictedAcc = map.PredictedAcc,
                ModifiersRating = map.ModifiersRating
            });

            Console.WriteLine($"PreBulk Leaderboards at {watch.Elapsed}");
            dbContext.Leaderboards.BulkInsertOptimized(leaderboards);
            Console.WriteLine($"Done importing Leaderboards at {watch.Elapsed}");

            var players = rootObject.Players.Select(player => new Player
            {
                Id = player.Id,
                Name = player.Name,
                Country = player.Country,
                Avatar = player.Avatar,
            });
            
            Console.WriteLine($"PreBulk Players at {watch.Elapsed}");
            dbContext.Players.BulkInsertOptimized(players);
            Console.WriteLine($"Done importing Players at {watch.Elapsed}");

            var scores = rootObject.Scores.Select(score => new Score
            {
                Id = score.Id,
                PlayerId = score.PlayerId,
                LeaderboardId = score.LeaderboardId,
                Accuracy = score.Accuracy,
                Modifiers = score.Modifiers
            });
            
            Console.WriteLine($"PreBulk Scores at {watch.Elapsed}");
            dbContext.Scores.BulkInsertOptimized(scores);
            Console.WriteLine($"Done importing Scores at {watch.Elapsed}");
        }
    }
}
