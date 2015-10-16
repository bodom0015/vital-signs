using System.ComponentModel;
using System.Threading;

namespace VitalSigns
{
    class EqCharInfo : INotifyPropertyChanged
    {
        private Timer combatTimer;

        #region Bindable Properties
        private string name = "";
        public string Name
        {
            get { return name; }
        }

        private string server = "";
        public string Server
        {
            get { return server; }
        }

        string currentZone = "";
        public string CurrentZone
        {
            get { return currentZone; }
            set
            {
                currentZone = value;
                NotifyPropertyChanged("CurrentZone");
            }
        }

        bool isLooting = false;
        public bool IsLooting
        {
            get { return isLooting; }
            set
            {
                isLooting = value;
                NotifyPropertyChanged("IsLooting");
            }
        }

        bool isRaiding = false;
        public bool IsRaiding
        {
            get { return isRaiding; }
            set
            {
                isRaiding = value;
                NotifyPropertyChanged("IsRaiding");
            }
        }

        int tellsSinceLastStatusChange = 0;
        public int TellAlerts
        {
            get { return tellsSinceLastStatusChange; }
            set
            {
                tellsSinceLastStatusChange = value;
                NotifyPropertyChanged("TellAlerts");
            }
        }

        int lootAwardedMsgsSinceLastStatusChange = 0;
        public int LootAlerts
        {
            get { return lootAwardedMsgsSinceLastStatusChange; }
            set
            {
                lootAwardedMsgsSinceLastStatusChange = value;
                NotifyPropertyChanged("LootAlerts");
            }
        }

        int auctionMsgsSinceLastStatusChange = 0;
        public int AuctionAlerts
        {
            get { return auctionMsgsSinceLastStatusChange; }
            set
            {
                auctionMsgsSinceLastStatusChange = value;
                NotifyPropertyChanged("AuctionAlerts");
            }
        }

        int guildMsgsSinceLastStatusChange = 0;
        public int GuildAlerts
        {
            get { return guildMsgsSinceLastStatusChange; }
            set
            {
                guildMsgsSinceLastStatusChange = value;
                NotifyPropertyChanged("GuildAlerts");
            }
        }

        int raidMsgsSinceLastStatusChange = 0;
        public int RaidAlerts
        {
            get { return raidMsgsSinceLastStatusChange; }
            set
            {
                raidMsgsSinceLastStatusChange = value;
                NotifyPropertyChanged("RaidAlerts");
            }
        }

        int raidChannelMsgsSinceLastStatusChange = 0;
        public int RaidChannelAlerts
        {
            get { return raidChannelMsgsSinceLastStatusChange; }
            set
            {
                raidChannelMsgsSinceLastStatusChange = value;
                NotifyPropertyChanged("RaidChannelAlerts");
            }
        }

        int hpChangeSinceLastStatusChange = 0;
        public int HpChange
        {
            get { return hpChangeSinceLastStatusChange; }
            set
            {
                hpChangeSinceLastStatusChange = value;
                NotifyPropertyChanged("HpChange");
            }
        }

        string status = "";
        public string Status
        {
            get { return status; }
            set 
            {
                status = value;
                NotifyPropertyChanged("Status");
                LootAlerts = 0;
                AuctionAlerts = 0;
                RaidAlerts = 0;
                RaidChannelAlerts = 0;
                GuildAlerts = 0;
                TellAlerts = 0;
                HpChange = 0;
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

        public EqCharInfo(string name, string server)
        {
            // Grab the character name / server
            this.name = name;
            this.server = server;

            // Initialize Status to "OK"
            this.ResetStatus();
        }

        #region Status Set / Reset Helpers
        public void SetCurrentZone(string newZoneName)
        {
            this.CurrentZone = newZoneName;
            this.ResetStatus();
        }

        public void SetAuctionStatus()
        {
            this.IsLooting = true;
            if (this.Status != "Loot Auction")
            {
                this.Status = "Loot Auction";
            }

            // Reset / create the timer
            this.combatTimer = new Timer((parameter) => { this.ResetStatus(); }, null, 1000 * 120, 0);
        }

        public void SetCombatStatus()
        {
            if (this.Status != "In Combat")
            {
                this.Status = "In Combat";
            }

            // Reset / create the timer
            this.combatTimer = new Timer((parameter) => { this.ResetStatus(); }, null, 1000 * 120, 0);
        }

        public void ResetStatus()
        {
            // Dispose the timer
            if (this.combatTimer != null)
            {
                this.combatTimer.Dispose();
            }

            // Set status based on HpChange (NOTE: this resets HpChange to 0)
            if (this.HpChange < -90000)
            {
                this.Status = "Gravely Wounded";
            }
            else if (this.HpChange < -60000)
            {
                this.Status = "Wounded";
            }
            else if (this.HpChange < -30000)
            {
                this.Status = "Injured";
            }
            else if (this.HpChange < -15000)
            {
                this.Status = "Tired";
            } 
            else if (this.HpChange >= -1000)
            {
                this.Status = this.IsRaiding ? "Raiding" : "OK";
            }
        }
        #endregion
    }
}
