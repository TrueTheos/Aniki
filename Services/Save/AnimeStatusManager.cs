using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aniki.Misc;
using Aniki.Models;

namespace Aniki.Services;

public class AnimeStatusManager
    {
        private readonly JsonDataManager<List<AnimeStatus>> _dataManager;
        public List<AnimeStatus> AnimeStatuses { get; private set; } = new();

        public AnimeStatusManager(string filePath)
        {
            _dataManager = new JsonDataManager<List<AnimeStatus>>(filePath);
            Load();
        }

        public void Load()
        {
            AnimeStatuses = _dataManager.Load(new List<AnimeStatus>());
        }

        public void Save()
        {
            _dataManager.Save(AnimeStatuses);
        }

        public void ChangeStatus(string animeName, AnimeStatusApi status)
        {
            var existingAnime = AnimeStatuses.FirstOrDefault(a => 
                a.Title.Equals(animeName, StringComparison.OrdinalIgnoreCase));
            
            if (existingAnime != null)
            {
                existingAnime.Status = status;
                Save();
            }
        }

        public void ChangeEpisodeCount(string animeName, int watchedEpisodes)
        {
            var existingAnime = AnimeStatuses.FirstOrDefault(a => 
                a.Title.Equals(animeName, StringComparison.OrdinalIgnoreCase));
            
            if (existingAnime != null)
            {
                existingAnime.WatchedEpisodes = watchedEpisodes;
                Save();
            }
        }

        public async Task SyncWithMal()
        {
            try
            {
                List<AnimeData>? animeList = await MalUtils.LoadAnimeList();

                if (animeList != null)
                {
                    AnimeStatuses.Clear();

                    foreach (AnimeData anime in animeList)
                    {
                        if (anime.Node != null)
                        {
                            AnimeStatuses.Add(new AnimeStatus
                            {
                                Title = anime.Node.Title,
                                WatchedEpisodes = anime.ListStatus.NumEpisodesWatched,
                                Status = anime.ListStatus.Status
                            });
                        }
                    }

                    Save();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error syncing with MAL: {ex.Message}");
            }
        }
    }