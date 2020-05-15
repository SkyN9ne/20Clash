﻿using System;
using ClashofClans.Extensions;
using ClashofClans.Files;
using ClashofClans.Files.Logic;
using ClashofClans.Logic.Manager.Items.Components;
using Newtonsoft.Json.Linq;

namespace ClashofClans.Logic.Manager.Items.GameObjects
{
    public class Building : GameObject
    {
        private int _upgradeLevel;
        public int Ammo;
        public bool AttackMode;
        public bool BoostPause;
        public int Data;
        public int Id;

        public bool Locked;

        public Building(Home.Home home) : base(home)
        {
        }

        public Timer ConstructionTimer { get; set; }
        public Timer BoostTimer { get; set; }

        public Buildings BuildingData => Csv.Tables.Get(Csv.Files.Buildings).GetDataWithId<Buildings>(Data);

        public ResourceProductionComponent ResourceProductionComponent =>
            TryGetComponent(5, out var component) ? (ResourceProductionComponent) component : null;

        public ResourceStorageComponent ResourceStorageComponent =>
            TryGetComponent(6, out var component) ? (ResourceStorageComponent) component : null;

        public void LoadComponents()
        {
            if (!string.IsNullOrEmpty(BuildingData.ProducesResource))
                AddComponent(new ResourceProductionComponent(this));

            if (BuildingData.CanStoreResources)
                AddComponent(new ResourceStorageComponent(this));

            if (BuildingData.UpgradesUnits) 
                AddComponent(new UnitUpgradeComponent(this));
        }

        public void StartUpgrade()
        {
            if (ConstructionTimer != null) return;
            var buildTime = BuildingData.GetBuildTime(_upgradeLevel + 1);

            // TODO: WORKER

            if (buildTime <= 0)
            {
                FinishConstruction();
            }
            else
            {
                ResourceProductionComponent?.CollectResources();

                ConstructionTimer = new Timer();
                ConstructionTimer.StartTimer(Home.Time, buildTime);
            }
        }

        public void StartBoost()
        {
            if (BoostTimer != null) return;

            // RESOURCE_PRODUCTION_BOOST_MULTIPLIER

            BoostTimer = new Timer();
            BoostTimer.StartTimer(Home.Time, 3600 * 24);
        }

        public void SpeedUpConstruction()
        {
            if (ConstructionTimer == null) return;
            var cost = GamePlayUtil.GetSpeedUpCost(ConstructionTimer.GetRemainingSeconds(Home.Time));

            if (Home.UseDiamonds(cost))
                FinishConstruction();
            else
                Logger.Log("Payment failed.", GetType(), Logger.ErrorLevel.Warning);
        }

        public int GetUpgradeLevel()
        {
            return _upgradeLevel;
        }

        public void SetUpgradeLevel(int upgradeLevel)
        {
            _upgradeLevel = upgradeLevel;

            var resourceProductionComponent = ResourceProductionComponent;
            resourceProductionComponent?.SetProduction();
        }

        public void FinishConstruction()
        {
            if (_upgradeLevel + 1 > BuildingData.MaxLevel)
            {
                SetUpgradeLevel(BuildingData.MaxLevel);
                Logger.Log($"Max level reached! [{BuildingData.Name}]", GetType(), Logger.ErrorLevel.Warning);
                return;
            }

            SetUpgradeLevel(_upgradeLevel + 1);

            // TODO: WORKER
            Home.AddExpPoints((int) Math.Sqrt(BuildingData.GetBuildTime(_upgradeLevel)));
            ConstructionTimer = null;

            if (_upgradeLevel == 0)
                Locked = false;
        }

        public override void FastForward(int seconds)
        {
            ConstructionTimer?.FastForward(seconds);
            BoostTimer?.FastForward(seconds);

            base.FastForward(seconds);
        }

        public override void Tick()
        {
            if (ConstructionTimer != null)
                if (ConstructionTimer.GetRemainingSeconds(Home.Time) <= 0)
                    FinishConstruction();

            if (BoostTimer != null)
                if (BoostTimer.GetRemainingSeconds(Home.Time) <= 0)
                    BoostTimer = null;

            base.Tick();
        }

        #region Json

        public override JObject Save()
        {
            var jObject = base.Save();
            jObject.Add("data", Data);
            jObject.Add("lvl", _upgradeLevel);

            if (Id > 0)
                jObject.Add("id", Id);

            if (ConstructionTimer != null)
                jObject.Add("const_t", ConstructionTimer.GetRemainingSeconds(Home.Time));

            if (BoostTimer != null)
                jObject.Add("boost_t", BoostTimer.GetRemainingSeconds(Home.Time));

            if (Locked)
                jObject.Add("locked", true);

            if (AttackMode)
                jObject.Add("attack_mode", true);

            if (BoostPause)
                jObject.Add("boost_pause", true);

            return jObject;
        }

        public override void Load(JObject jObject)
        {
            Data = jObject["data"].ToObject<int>();
            SetUpgradeLevel(jObject["lvl"].ToObject<int>());

            if (jObject.ContainsKey("const_t"))
            {
                var constructionTime = jObject["const_t"].ToObject<int>();
                if (constructionTime > -1)
                {
                    constructionTime = Math.Min(constructionTime, BuildingData.GetBuildTime(_upgradeLevel + 1));

                    ConstructionTimer = new Timer();
                    ConstructionTimer.StartTimer(Home.Time, constructionTime);
                    // TODO: WORKER
                }
            }

            if (jObject.ContainsKey("boost_t"))
            {
                var boostTime = jObject["boost_t"].ToObject<int>();
                if (boostTime > -1)
                {
                    BoostTimer = new Timer();
                    BoostTimer.StartTimer(Home.Time, boostTime);
                }
            }

            if (jObject.ContainsKey("ammo"))
                Ammo = jObject["ammo"].ToObject<int>();

            if (jObject.ContainsKey("locked"))
                Locked = jObject["locked"].ToObject<bool>();

            if (jObject.ContainsKey("attack_mode"))
                AttackMode = jObject["attack_mode"].ToObject<bool>();

            if (jObject.ContainsKey("attack_mode"))
                AttackMode = jObject["attack_mode"].ToObject<bool>();

            if (jObject.ContainsKey("boost_pause"))
                BoostPause = jObject["boost_pause"].ToObject<bool>();

            LoadComponents();

            base.Load(jObject);
        }

        #endregion
    }
}