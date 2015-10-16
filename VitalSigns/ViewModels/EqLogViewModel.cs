using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace VitalSigns
{
    class EqLogViewModel : INotifyPropertyChanged, IDisposable
    {
        #region Regex Helpers
        // Status patterns
        readonly static Regex NEW_ZONE_PATTERN = new Regex(@"You have entered (?!an area where levitation effects do not function)(.+)\.");
        readonly static Regex AFK_ON_PATTERN = new Regex(@"You are now A\.F\.K\. \(Away From Keyboard\)\.");
        readonly static Regex AFK_OFF_PATTERN = new Regex(@"You are no longer A\.F\.K\. \(Away From Keyboard\)\.");
        readonly static Regex FD_SUCCESS_PATTERN = new Regex(@"(Your enemies have forgotten you\!|You escape from combat.*)");

        // Counter patterns
        readonly static Regex TELL_RECV_PATTERN = new Regex(@".+ -> .+: .*");
        readonly static Regex GUILD_MSG_PATTERN = new Regex(@".+ tells the guild, '.*'");

        // Raid-only status patterns
        readonly static Regex RAID_ON_PATTERN = new Regex(@"You have joined the raid\.");
        readonly static Regex RAID_OFF_PATTERN = new Regex(@"(You have left the raid\.|You were removed from the raid\.|Your raid has been disbanded\.|Welcome to EverQuest\!|(Drakang|Swarmy|Powerful|Scornful) tells the raid, '.*[Ee]nd [Oo]f [Rr]aid.*')");
        readonly static Regex RAID_TRIGGER_PATTERN = new Regex(@".+ tells the raid, '.*((trigger|we('?re| are) (going|triggering))|[Ww][Tt][Ff][Uu]).*'");

        // Raid-only counters
        readonly static Regex RAID_MSG_PATTERN = new Regex(@".+ tells the raid, '.*'");
        readonly static Regex RAID_CHANNEL_PATTERN = new Regex(@".+ tells tsmraid:\d+, '.*'");

        // Raid-only loot-only counters
        readonly static Regex RAID_DKP_AUCTION_PATTERN = new Regex(@".+ tells the raid, '.*[Tt]ells.* \d+\s?[Dd][Kk][Pp] [Mm][Ii][Nn].*'");
        readonly static Regex RAID_DKP_LOOT_AWARDED_PATTERN = new Regex(@".+ tells the raid, '.*[Gg][Rr][Aa][Tt][SsZz].* \d+\s?[Dd][Kk][Pp].*'");

        // Damage patterns
        readonly static Regex healPattern = new Regex(@"([Yy]ou have been healed for \d+ points|.+ healed you for \d+ points)");
        readonly static Regex meleeDmgPattern = new Regex(@".+ (bites|crushes|punches|kicks|slams|bashes|pierces|hits|slashes) [Yy][Oo][Uu] for \d+ points of damage\.");
        readonly static Regex spellDmgPattern = new Regex(@"You have taken \d+( points of)? damage");
        readonly static Regex damagePattern = new Regex(@"\d+ points( of damage)?");

        // Simple regex <-> Action pairs (no capture)
        Dictionary<Regex, Action> _cachedRegexActions;
        Dictionary<Regex, Action> regexActions {
            get
            {
                if (_cachedRegexActions != null && _cachedRegexActions.Count > 0)
                {
                    return _cachedRegexActions;
                }
                _cachedRegexActions = new Dictionary<Regex, Action>();
                _cachedRegexActions.Add(TELL_RECV_PATTERN, () => { Subject.TellAlerts++; });
                _cachedRegexActions.Add(GUILD_MSG_PATTERN, () => { Subject.GuildAlerts++; });
                _cachedRegexActions.Add(AFK_ON_PATTERN, () => { Subject.Status = "AFK"; });
                _cachedRegexActions.Add(AFK_OFF_PATTERN, () => { Subject.ResetStatus(); });
                _cachedRegexActions.Add(FD_SUCCESS_PATTERN, () => { Subject.ResetStatus(); });
                _cachedRegexActions.Add(RAID_ON_PATTERN, () => { Subject.IsRaiding = true; Subject.ResetStatus(); });
                return _cachedRegexActions;
            }
        }

        // Simple regex <-> Action pairs (no capture) for raid-only and loot-only

        Dictionary<Regex, Action> _cachedRaidRegexActions;
        Dictionary<Regex, Action> raidRegexActions {
            get
            {
                if (_cachedRaidRegexActions != null && _cachedRaidRegexActions.Count > 0)
                {
                    return _cachedRaidRegexActions;
                }

                _cachedRaidRegexActions = new Dictionary<Regex, Action>();
                _cachedRaidRegexActions.Add(RAID_DKP_AUCTION_PATTERN, () => {  Subject.AuctionAlerts++; Subject.SetAuctionStatus(); });
                _cachedRaidRegexActions.Add(RAID_DKP_LOOT_AWARDED_PATTERN, () => { Subject.LootAlerts++; Subject.SetAuctionStatus(); });
                _cachedRaidRegexActions.Add(RAID_OFF_PATTERN, () => { Subject.IsRaiding = false; Subject.ResetStatus(); });
                _cachedRaidRegexActions.Add(RAID_TRIGGER_PATTERN, () => { Subject.Status = "RAID EVENT STARTED"; });
                _cachedRaidRegexActions.Add(RAID_MSG_PATTERN, () => { Subject.RaidAlerts++; });
                _cachedRaidRegexActions.Add(RAID_CHANNEL_PATTERN, () => { Subject.RaidChannelAlerts++; });
                return _cachedRaidRegexActions;
            }
        }

        private static string GetFirstCaptureOrNull(Regex pattern, string contents)
        {
            CaptureCollection captures = pattern.Match(contents).Captures;
            if (captures.Count == 1)
            {
                return captures[0].Value;
            }
            return null;
        }
        #endregion

        #region Bindable Properties
        private EqCharInfo _subject;
        public EqCharInfo Subject {
            get
            {
                return _subject;
            }
            set
            {
                _subject = value;
                NotifyPropertyChanged("Subject");
            }
        }
        #endregion

        #region PropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        public void NotifyPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }
        #endregion

        #region Internal State
        // File Stuff
        readonly static string timestampMask = "ddd MMM dd HH:mm:ss yyyy";
        readonly string logDirectory;
        readonly string logFileName;
        readonly string fullPathToLogFile;
        FileSystemWatcher watcher;

        // State-Tracking Stuff
        int lastIndex = 0;
        List<string> lines;
        Dictionary<DateTime, List<string>> lineMap;
        #endregion

        // On Startup:
        public EqLogViewModel(string fullPath, bool broadcast = true)
        {
            // Dissect full path for directory / fileName
            this.fullPathToLogFile = fullPath;
            string[] pathSegments = fullPath.Split(new char[] { '\\' });
            this.logFileName = pathSegments[pathSegments.Count() - 1];
            this.logDirectory = Regex.Replace(fullPath, this.logFileName, "");

            // Store the filename, and split out the identifier / character / server
            string[] segments = this.logFileName.Split(new char[] { '_' });

            // Ensure this file is a valid EverQuest Log file
            if (segments.Length == 3 && segments[0].EndsWith("eqlog"))
            {
                // Grab the character name / server
                var name = segments[1];
                var server = segments[2].Split(new char[] { '.' })[0];

                this.Subject = new EqCharInfo(name, server);
            }
            else
            {
                throw new ArgumentException("Invalid log file selected. Please choose a valid EverQuest log file.");
            }

            // Initialize the stores of lines
            lines = new List<string>();
            lineMap = new Dictionary<DateTime, List<string>>();

            // Poll once to initialize limits
            SkipToEndOfFile();

            // Set up our FileSystemWatcher to watch the file's directory
            watcher = new FileSystemWatcher(this.logDirectory, "*.txt");
            watcher.Changed += watcher_Changed;

            // Start polling the file
            watcher.EnableRaisingEvents = true;
        }

        public void Dispose()
        {
            // TODO: Dispose of transmission protocol stuff here (sockets, etc)
        }

        // On File Change:
        void watcher_Changed(object sender, FileSystemEventArgs e)
        {            
            if (e.ChangeType == WatcherChangeTypes.Changed && e.FullPath == this.fullPathToLogFile)
            {
                Debug.WriteLine("File updated! Reading from " + this.lastIndex + " to end.");

                // Read the file to find upper bound
                lines.AddRange(this.ReadFromLastIndexToEnd());

                // Mark our new file ending so we know where to start next time
                this.lastIndex = lines.Count;
            }
        }

        #region File Operations
        private FileStream OpenFile(int n = 0)
        {
            // Open a ReadWrite FileStream
            var fs = new FileStream(this.fullPathToLogFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (n > 0)
            {
                long whatIsThisNumber = fs.Seek(n, SeekOrigin.Begin);
            }

            return fs;
        }

        private void SkipToEndOfFile()
        {
            // Read all lines, but do not parse them
            lines.AddRange(this.ReadFromLastIndexToEnd(true));

            // Initialize lastIndex so we know where to start parsing
            this.lastIndex = lines.Count;
        }

        private List<string> ReadFromLastIndexToEnd(bool init = false)
        {
            // Save the new lines of the file
            List<string> newLines = new List<string>();
            int count = 0;

            // Open a File Stream
            using (var fs = OpenFile())
            using (var sr = new StreamReader(fs))
            {
                while (!sr.EndOfStream)
                {
                    string line = sr.ReadLine();

                    if (count++ < this.lastIndex && !lines.Contains(line))
                    {
                        continue;
                    }
                    newLines.Add(line);

                    // Split up the timestamp and the line data
                    string[] segments = line.Split(new char[] { '[', ']' });

                    if (segments.Length == 3)
                    {
                        // Parse out the timestamp and return it as a DateTime
                        DateTime timestamp = DateTime.ParseExact(segments[1], timestampMask, CultureInfo.CurrentCulture);

                        // Trim off the excess whitespace
                        string lineContents = segments[2].Trim();

                        // Initialize this timestamp in the map if we haven't seen it before
                        if (!lineMap.ContainsKey(timestamp))
                        {
                            this.lineMap[timestamp] = new List<string>();
                        }

                        // If this is a new line, record the line and operate on it
                        if (lineMap[timestamp].Contains(lineContents))
                        {
                            // Duplicate detected!
                        }

                        this.lineMap[timestamp].Add(lineContents);

                        // Operate on the contents and update stats here
                        if (init)
                        {
                            // We still want to update CurrentZone on initialization
                            string zoneCapture = GetFirstCaptureOrNull(NEW_ZONE_PATTERN, lineContents);
                            if (zoneCapture != null)
                            {
                                string newZoneName = zoneCapture.Replace("You have entered ", "").Replace(".", "");
                                Subject.SetCurrentZone(newZoneName);
                            }
                        }
                        else
                        {
                            // Parse all incoming lines
                            this.ParseContents(timestamp, lineContents);
                        }
                    }
                    else
                    {
                        // Malformed line!
                    }
                }
            }

            return newLines;
        }
        #endregion

        private void ParseContents(DateTime timestamp, string contents)
        {
            // Run the simple patterns first
            foreach (Regex regexPattern in this.regexActions.Keys)
            {
                if (regexPattern.IsMatch(contents))
                {
                    this.regexActions[regexPattern]();
                    return;
                }
            }

            // Run the raid-only patterns next, if necessary
            if (Subject.IsRaiding)
            {
                foreach (Regex regexPattern in this.raidRegexActions.Keys)
                {
                    if (regexPattern.IsMatch(contents))
                    {
                        this.raidRegexActions[regexPattern]();
                        return;
                    }
                }
            }

            // Then run the more complex patterns:
            #region Zone Change
            string zoneCapture = GetFirstCaptureOrNull(NEW_ZONE_PATTERN, contents);
            if (zoneCapture != null)
            {
                string newZoneName = zoneCapture.Replace("You have entered ", "").Replace(".", "");
                Subject.SetCurrentZone(newZoneName);
                return;
            }
            #endregion

            #region Damage / Healing
            string dmgSubLine = GetFirstCaptureOrNull(damagePattern, contents);

            if (dmgSubLine != null)
            {
                // Store the hpChange resulting from this line (signed int32)
                int hpChange = 0;

                // Skip this line if it does not affect the current character
                if (!contents.Contains(Subject.Name) && !contents.Contains("you") && !contents.Contains("YOU") && !contents.Contains("You"))
                {
                    return;
                }

                // If we cannot parse this integer properly, skip this line
                if (!Int32.TryParse(dmgSubLine.Split(new char[] { ' ' })[0], out hpChange))
                {
                    return;
                }

                // Tally up the hp change resulting from this message
                if (healPattern.IsMatch(contents))
                {
                    Subject.SetCombatStatus();
                    Subject.HpChange += hpChange;
                }
                else if (meleeDmgPattern.IsMatch(contents) || spellDmgPattern.IsMatch(contents))
                {
                    Subject.SetCombatStatus();
                    Subject.HpChange -= hpChange;
                }

                return;
            }
            #endregion
        }
    }
}
