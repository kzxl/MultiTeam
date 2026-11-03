using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using MultiTeam.Models;

namespace MultiTeam.Services
{
    public class AccountStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        public string BaseDirectory { get; }
        public string SessionsDirectory { get; }
        public string ConfigFilePath { get; }

        public AccountStore()
        {
            BaseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            SessionsDirectory = Path.Combine(BaseDirectory, "Sessions");
            ConfigFilePath = Path.Combine(SessionsDirectory, "accounts.json");

            if (!Directory.Exists(SessionsDirectory))
            {
                Directory.CreateDirectory(SessionsDirectory);
            }
        }

        public List<TeamAccount> LoadAccounts()
        {
            try
            {
                if (File.Exists(ConfigFilePath))
                {
                    string json = File.ReadAllText(ConfigFilePath);
                    var accounts = JsonSerializer.Deserialize<List<TeamAccount>>(json, JsonOptions);
                    if (accounts != null && accounts.Count > 0)
                    {
                        return accounts;
                    }
                }
            }
            catch (Exception)
            {
                // Fallback to default if config file is corrupted
            }

            // Seed default account
            var defaultList = new List<TeamAccount>
            {
                new TeamAccount
                {
                    Id = "acc_01",
                    Name = "Tài khoản 1",
                    CreatedAt = DateTime.Now
                }
            };
            SaveAccounts(defaultList);
            return defaultList;
        }

        public void SaveAccounts(IEnumerable<TeamAccount> accounts)
        {
            try
            {
                string json = JsonSerializer.Serialize(accounts, JsonOptions);
                File.WriteAllText(ConfigFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save accounts: {ex.Message}");
            }
        }

        public string GetProfileFolder(string accountId)
        {
            string profileFolder = Path.Combine(SessionsDirectory, accountId);
            if (!Directory.Exists(profileFolder))
            {
                Directory.CreateDirectory(profileFolder);
            }
            return profileFolder;
        }

        public void DeleteProfileData(string accountId)
        {
            try
            {
                string profileFolder = Path.Combine(SessionsDirectory, accountId);
                if (Directory.Exists(profileFolder))
                {
                    Directory.Delete(profileFolder, true);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error deleting profile data for {accountId}: {ex.Message}");
            }
        }
    }
}
