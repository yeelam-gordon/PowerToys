// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;

namespace PowerAccent.Core.Tools
{
    public class CharactersUsageInfo
    {
        [JsonPropertyName("characterUsageCounters")]
        public Dictionary<string, uint> _characterUsageCounters { get; set; } = new Dictionary<string, uint>();
        
        [JsonPropertyName("characterUsageTimestamp")]
        public Dictionary<string, long> _characterUsageTimestamp { get; set; } = new Dictionary<string, long>();

        private const string PowerAccentModuleName = "QuickAccent";
        private const string UsageDataFileName = "usage_data.json";
        private int _changeCounter = 0;
        private const int SaveFrequency = 10;

        public bool Empty()
        {
            return _characterUsageCounters.Count == 0;
        }

        public void Clear()
        {
            _characterUsageCounters.Clear();
            _characterUsageTimestamp.Clear();
        }

        public uint GetUsageFrequency(string character)
        {
            _characterUsageCounters.TryGetValue(character, out uint frequency);

            return frequency;
        }

        public long GetLastUsageTimestamp(string character)
        {
            _characterUsageTimestamp.TryGetValue(character, out long timestamp);

            return timestamp;
        }

        public void IncrementUsageFrequency(string character)
        {
            if (_characterUsageCounters.TryGetValue(character, out uint currentCount))
            {
                _characterUsageCounters[character] = currentCount + 1;
            }
            else
            {
                _characterUsageCounters[character] = 1;
            }

            _characterUsageTimestamp[character] = DateTimeOffset.Now.ToUnixTimeSeconds();
            
            // Increment change counter
            _changeCounter++;
            
            // Only save after certain number of changes
            if (_changeCounter >= SaveFrequency)
            {
                SaveUsageData();
                _changeCounter = 0;
            }
        }

        public void LoadUsageData()
        {
            try
            {
                string settingsPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Microsoft",
                    "PowerToys",
                    PowerAccentModuleName);
                
                string filePath = Path.Combine(settingsPath, UsageDataFileName);
                
                if (File.Exists(filePath))
                {
                    string jsonString = File.ReadAllText(filePath);
                    var loadedData = JsonSerializer.Deserialize<CharactersUsageInfo>(jsonString);
                    if (loadedData != null)
                    {
                        // Create temporary dictionaries and only assign on successful load
                        var tempCounters = loadedData._characterUsageCounters;
                        var tempTimestamps = loadedData._characterUsageTimestamp;
                        
                        // Only replace the existing data if loading was successful
                        _characterUsageCounters = tempCounters;
                        _characterUsageTimestamp = tempTimestamps;
                    }
                }
            }
            catch (Exception)
            {
                // If loading fails, continue with existing data
                // No Clear() call here to preserve existing data
            }
        }

        public void SaveUsageData()
        {
            try
            {
                string settingsPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Microsoft", 
                    "PowerToys",
                    PowerAccentModuleName);
                
                // Create the directory if it doesn't exist
                if (!Directory.Exists(settingsPath))
                {
                    Directory.CreateDirectory(settingsPath);
                }
                
                string filePath = Path.Combine(settingsPath, UsageDataFileName);
                string jsonString = JsonSerializer.Serialize(this);
                File.WriteAllText(filePath, jsonString);
                
                // Reset the counter after successful save
                _changeCounter = 0;
            }
            catch (Exception)
            {
                // Silently fail if saving doesn't work
            }
        }
    }
}
