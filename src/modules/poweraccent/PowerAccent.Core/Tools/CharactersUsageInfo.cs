// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
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
        
        // Object for thread synchronization
        private readonly object _lockObj = new object();
        
        // Task to track the current save operation
        private Task _currentSaveTask = Task.CompletedTask;

        public bool Empty()
        {
            lock (_lockObj)
            {
                return _characterUsageCounters.Count == 0;
            }
        }

        public void Clear()
        {
            lock (_lockObj)
            {
                _characterUsageCounters.Clear();
                _characterUsageTimestamp.Clear();
            }
        }

        public uint GetUsageFrequency(string character)
        {
            lock (_lockObj)
            {
                _characterUsageCounters.TryGetValue(character, out uint frequency);
                return frequency;
            }
        }

        public long GetLastUsageTimestamp(string character)
        {
            lock (_lockObj)
            {
                _characterUsageTimestamp.TryGetValue(character, out long timestamp);
                return timestamp;
            }
        }

        public void IncrementUsageFrequency(string character)
        {
            lock (_lockObj)
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
                    // Start save operation without awaiting (fire and forget)
                    SaveUsageDataAsync();
                    _changeCounter = 0;
                }
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
                        lock (_lockObj)
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
            }
            catch (Exception)
            {
                // If loading fails, continue with existing data
                // No Clear() call here to preserve existing data
            }
        }

        // Public method that triggers the async save operation
        public void SaveUsageData()
        {
            // Start save operation without awaiting (fire and forget)
            SaveUsageDataAsync();
        }
        
        // Private async implementation that does the actual saving
        private Task SaveUsageDataAsync()
        {
            // Make a copy of the data to save while holding the lock
            Dictionary<string, uint> countersCopy;
            Dictionary<string, long> timestampsCopy;
            
            lock (_lockObj)
            {
                // We only lock while making a copy of the data
                countersCopy = new Dictionary<string, uint>(_characterUsageCounters);
                timestampsCopy = new Dictionary<string, long>(_characterUsageTimestamp);
            }
            
            // Store a class to serialize with the copied data
            var dataToSave = new CharactersUsageInfo
            {
                _characterUsageCounters = countersCopy,
                _characterUsageTimestamp = timestampsCopy
            };
            
            // Lock to ensure only one save operation runs at a time
            lock (_currentSaveTask)
            {
                // If the previous task is still running, wait for it to complete
                if (!_currentSaveTask.IsCompleted)
                {
                    try
                    {
                        // Try to wait for it, but don't block indefinitely
                        _currentSaveTask.Wait(100);
                    }
                    catch (Exception)
                    {
                        // Ignore exceptions from the previous task
                    }
                }
                
                // Create a new task for the save operation
                _currentSaveTask = Task.Run(async () =>
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
                        string jsonString = JsonSerializer.Serialize(dataToSave);
                        
                        // Use async file operations to avoid blocking
                        await File.WriteAllTextAsync(filePath, jsonString);
                    }
                    catch (Exception)
                    {
                        // Silently fail if saving doesn't work
                    }
                });
                
                return _currentSaveTask;
            }
        }
    }
}
