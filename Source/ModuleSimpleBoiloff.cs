﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;


namespace SimpleBoiloff
{
    public class ModuleCryoTank: PartModule
    {
        // Name of the fuel to boil off
        [KSPField(isPersistant = false)]
        public string FuelName;

        [KSPField(isPersistant = false)]
        public double FuelTotal;

        // Rate of boiling off in %/hr
        [KSPField(isPersistant = false)]
        public float BoiloffRate = 0.025f;

        // Cost to cool off u/s per 1000 u
        [KSPField(isPersistant = false)]
        public float CoolingCost = 0.0f;

        // Last timestamp that boiloff occurred
        [KSPField(isPersistant = true)]
        public double LastUpdateTime = 0;

        // Whether active tank refrigeration is occurring
        [KSPField(isPersistant = true)]
        public bool CoolingEnabled = true;

        [KSPField(isPersistant = true)]
        public bool BoiloffOccuring = false;



        // PRIVATE
        private double fuelAmount = 0.0;
        private double maxFuelAmount = 0.0;
        private double coolingCost = 0.0;
        private double boiloffRateSeconds = 0.0;

        // UI FIELDS/ BUTTONS
        // Status string
        [KSPField(isPersistant = false, guiActive = true, guiName = "Boiloff")]
        public string BoiloffStatus = "N/A";

        [KSPField(isPersistant = false, guiActive = false, guiName = "Insulation")]
        public string CoolingStatus = "N/A";

        [KSPEvent(guiActive = false, guiName = "Enable Cooling", active = true)]
        public void Enable()
        {
            CoolingEnabled = true;
        }
        [KSPEvent(guiActive = false, guiName = "Disable Cooling", active = false)]
        public void Disable()
        {
            CoolingEnabled = false;
        }

        // ACTIONS
        [KSPAction("Enable Cooling")]
        public void EnableAction(KSPActionParam param) { Enable(); }

        [KSPAction("Disable Cooling")]
        public void DisableAction(KSPActionParam param) { Disable(); }

        [KSPAction("Toggle Cooling")]
        public void ToggleAction(KSPActionParam param)
        {
            CoolingEnabled = !CoolingEnabled;
        }

        public override string GetInfo()
        {
          Debug.Log("GETINFO ");
          string msg = String.Format("Loss Rate: {0:F2}% {1}/hr", BoiloffRate, FuelName);
            if (CoolingCost > 0.0f)
            {
               
                msg += String.Format("\nCooling Cost: {0:F2} Ec/s", CoolingCost*(float)(FuelTotal/1000.0));
            }
          return msg;
        }

        public void Start()
        {

            if (HighLogic.LoadedSceneIsFlight)
            {
              maxFuelAmount = GetMaxResourceAmount(FuelName);

              boiloffRateSeconds = BoiloffRate/100.0/3600.0;
              if (CoolingCost > 0.0)
              {
                coolingCost = maxFuelAmount/1000.0 * CoolingCost;
                Events["Disable"].guiActive = true;
                Events["Enable"].guiActive = true;
              }
              // Catchup
              DoCatchup();
            }
        }

        public void DoCatchup()
        {
          if (part.vessel.missionTime > 0.0)
          {
              if (part.RequestResource("ElectricCharge", coolingCost * TimeWarp.fixedDeltaTime) < coolingCost * TimeWarp.fixedDeltaTime)
              {
                  double elapsedTime = part.vessel.missionTime - LastUpdateTime;

                  double toBoil = Math.Pow(1.0 - boiloffRateSeconds, elapsedTime);
                  part.RequestResource(FuelName, (1.0 - toBoil) * fuelAmount,ResourceFlowMode.NO_FLOW);
              }
          }
        }

        public void Update()
        {
          if (HighLogic.LoadedSceneIsFlight)
          {
            // Show the insulation status field if there is a cooling cost
            if (CoolingCost > 0f)
            {
              foreach (BaseField fld in base.Fields)
                {
                    if (fld.guiName == "Insulation")
                        fld.guiActive = true;
                }

              if (Events["Enable"].active == CoolingEnabled || Events["Disable"].active != CoolingEnabled)
                {
                    Events["Disable"].active = CoolingEnabled;
                    Events["Enable"].active = !CoolingEnabled;
               }
            }
            if (fuelAmount == 0.0)
            {
                foreach (BaseField fld in base.Fields)
                {
                    if (fld.guiName == "Boiloff")
                        fld.guiActive = false;
                }

            }

          }
          if (HighLogic.LoadedSceneIsEditor)
          {
              if (CoolingCost > 0f)
              {
                  foreach (BaseField fld in base.Fields)
                  {
                      if (fld.guiName == "Insulation")
                          fld.guiActiveEditor = true;
                  }
                  double max = GetMaxResourceAmount(FuelName);
                  CoolingStatus = String.Format("Cost {0:F2} Ec/s", CoolingCost * (float)(max / 1000.0));
              }
          }
        }
        protected void FixedUpdate()
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                fuelAmount = GetResourceAmount(FuelName);
                // If we have no fuel, no need to do any calculations
                if (fuelAmount == 0.0)
                {
                    BoiloffStatus = "No Fuel";
                    CoolingStatus = "No Fuel";
                    return;
                }

                // If the cooling cost is zero, we must boil off
                if (coolingCost == 0f)
                {
                    BoiloffOccuring = true;
                    BoiloffStatus = FormatRate(boiloffRateSeconds* fuelAmount);
                }
                // else check for available power
                else
                {
                    if (!CoolingEnabled)
                    {
                        BoiloffOccuring = true;
                        BoiloffStatus = FormatRate(boiloffRateSeconds * fuelAmount);
                        CoolingStatus = "Disabled";
                    }
                  }

                if (BoiloffOccuring)
                {
                    DoBoiloff(1d);
                }
                if (part.vessel.missionTime > 0.0)
                {
                    LastUpdateTime = part.vessel.missionTime;
                }
            }
        }
        // Returns the cooling cost if the system is enabled
        public double GetCoolingCost()
        {
          if (CoolingEnabled)
          {
            return coolingCost;
          }
          return 0d;
        }

        public double SetBoiloffState(bool state)
        {
          if (CoolingEnabled && coolingCost > 0f)
          {
            if (state)
            {
              BoiloffOccuring = true;
              BoiloffStatus = FormatRate(boiloffRateSeconds * fuelAmount);
              CoolingStatus = "Uncooled!";
            } else
            {

              BoiloffOccuring = false;
              BoiloffStatus = String.Format("Insulated");
              CoolingStatus = String.Format("Using {0:F2} Ec/s", coolingCost);
            }
            return (double)coolingCost;
        }
        return 0d;


        }
        public void TryConsumeCharge()
        {
            if (CoolingEnabled && coolingCost > 0f)
            {
                double chargeRequest = coolingCost * TimeWarp.fixedDeltaTime;
                double req = part.RequestResource("ElectricCharge", chargeRequest);
                double tolerance = 0.0001;
                if (req >= chargeRequest - tolerance)
                {
                    SetBoiloffState(false);
                } else
                {
                    SetBoiloffState(true);
                }
            }
        }

        public void ConsumeCharge()
        {
          if (CoolingEnabled && coolingCost > 0f)
          {
            double chargeRequest = coolingCost * TimeWarp.fixedDeltaTime;

            double req = part.RequestResource("ElectricCharge", chargeRequest);
            //Debug.Log(req.ToString() + " rec, wanted "+ chargeRequest.ToString());
            // Fully cooled
            double tolerance = 0.0001;
            if (req >= chargeRequest - tolerance)
            {
                BoiloffOccuring = false;
                BoiloffStatus = String.Format("Insulated");
                CoolingStatus = String.Format("Using {0:F2} Ec/s", coolingCost);
            }
            else
            {
                BoiloffOccuring = true;
                BoiloffStatus = FormatRate(boiloffRateSeconds * fuelAmount);
                CoolingStatus = "Uncooled!";
            }
          }
        }


        protected void DoBoiloff(double scale)
        {
            // 0.025/100/3600
      		double toBoil = Math.Pow(1.0-boiloffRateSeconds, TimeWarp.fixedDeltaTime)*scale;
      		boiled = part.RequestResource(FuelName, (1.0-toBoil) * fuelAmount,ResourceFlowMode.NO_FLOW );
        }

        private double boiled = 0d;

        protected string FormatRate(double rate)
        {
            double adjRate = rate;
            string interval = "s";
            if (adjRate < 0.01)
            {
                adjRate = adjRate*60.0;
                interval = "min";
            }
            if (adjRate < 0.01)
            {
                adjRate = adjRate * 60.0;
                interval = "hr";
            }
            return String.Format("Losing {0:F2} u/{1}", adjRate, interval);
        }

        protected double GetResourceAmount(string nm)
        {


            PartResource res = this.part.Resources.Get(PartResourceLibrary.Instance.GetDefinition(nm).id);
            return res.amount;
        }
        protected double GetMaxResourceAmount(string nm)
        {
            
            int id = PartResourceLibrary.Instance.GetDefinition(nm).id;
            
            PartResource res = this.part.Resources.Get(id);
            Debug.Log(res);
            return res.maxAmount;
        }

    }
}
