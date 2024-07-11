using System;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Collections.Concurrent;

namespace portaBLe
{
    public static class DataImporter
    {
        public static void ImportJsonData(RootObject rootObject, AppContext dbContext)
        {
            dbContext.ChangeTracker.AutoDetectChangesEnabled = false;

            foreach (var item in rootObject.Maps)
            {
                if (item.ModifiersRating != null) {
                    item.ModifiersRating.Id = 0;
                }
            }

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
            }).ToList();

            dbContext.Leaderboards.AddRange(leaderboards);
            dbContext.BulkSaveChanges();

            var players = rootObject.Players.Select(player => new Player
            {
                Id = player.Id,
                Name = player.Name,
                Country = player.Country,
                Avatar = player.Avatar,
            }).ToList();

            dbContext.Players.AddRange(players);
            dbContext.BulkSaveChanges();

            var scores = rootObject.Scores.Select(score => new Score
            {
                Id = score.Id,
                PlayerId = score.PlayerId,
                LeaderboardId = score.LeaderboardId,
                Accuracy = score.Accuracy,
                Modifiers = score.Modifiers
            }).ToList();

            dbContext.Scores.AddRange(scores);

            dbContext.BulkSaveChanges();
        }
    }
}
