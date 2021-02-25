﻿using ADepIn;
using BepInEx.Configuration;
using Deli;
using FistVR;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TNHTweaker.ObjectTemplates;
using TNHTweaker.Utilities;
using UnityEngine;
using UnityEngine.UI;

namespace TNHTweaker
{
    public class TNHTweaker : DeliBehaviour
    {

        private static ConfigEntry<bool> printCharacters;
        private static ConfigEntry<bool> logTNH;
        private static ConfigEntry<bool> logFileReads;
        private static ConfigEntry<bool> allowLog;

        private static string OutputFilePath;

        private static List<int> spawnedBossIndexes = new List<int>();
        private static List<GameObject> SpawnedConstructors = new List<GameObject>();
        private static List<GameObject> SpawnedPanels = new List<GameObject>();

        private static bool filesBuilt = false;
        private static bool preventOutfitFunctionality = false;

        ///////////////////////////////////////////////
        //INITIALIZING THE TAKE AND HOLD TWEAKER PLUGIN
        ///////////////////////////////////////////////

        /// <summary>
        /// First method that gets called
        /// </summary>
        private void Awake()
        {
            TNHTweakerLogger.Init();
            TNHTweakerLogger.Log("Hello World (from TNH Tweaker)", TNHTweakerLogger.LogType.General);

            Harmony.CreateAndPatchAll(typeof(TNHTweaker));

            LoadConfigFile();

            SetupOutputDirectory();

            LoadPanelSprites();
        }

        /// <summary>
        /// Loads the sprites used in secondary panels in TNH
        /// </summary>
        private void LoadPanelSprites()
        {
            Option<Texture2D> magUpgradeContent = Source.Resources.Get<Texture2D>("mag_upgrade.png");
            LoadedTemplateManager.PanelSprites.Add(PanelType.MagUpgrader, TNHTweakerUtils.LoadSprite(magUpgradeContent.Expect("TNHTweaker -- Failed to load Mag Upgrader icon!")));

            Option<Texture2D> fullAutoContent = Source.Resources.Get<Texture2D>("full_auto.png");
            LoadedTemplateManager.PanelSprites.Add(PanelType.AddFullAuto, TNHTweakerUtils.LoadSprite(fullAutoContent.Expect("TNHTweaker -- Failed to load Full Auto Adder icon!")));

            Option<Texture2D> ammoPurchaseContent = Source.Resources.Get<Texture2D>("ammo_purchase.png");
            LoadedTemplateManager.PanelSprites.Add(PanelType.AmmoPurchase, TNHTweakerUtils.LoadSprite(ammoPurchaseContent.Expect("TNHTweaker -- Failed to load Ammo Purchase icon!")));

            Option<Texture2D> magPurchaseContent = Source.Resources.Get<Texture2D>("mag_purchase.png");
            LoadedTemplateManager.PanelSprites.Add(PanelType.MagPurchase, TNHTweakerUtils.LoadSprite(magPurchaseContent.Expect("TNHTweaker -- Failed to load Mag Purchase icon!")));

            Option<Texture2D> fireRateUpContent = Source.Resources.Get<Texture2D>("gas_up.png");
            LoadedTemplateManager.PanelSprites.Add(PanelType.FireRateUp, TNHTweakerUtils.LoadSprite(fireRateUpContent.Expect("TNHTweaker -- Failed to load Fire Rate Up icon!")));

            Option<Texture2D> fireRateDownContent = Source.Resources.Get<Texture2D>("gas_down.png");
            LoadedTemplateManager.PanelSprites.Add(PanelType.FireRateDown, TNHTweakerUtils.LoadSprite(fireRateDownContent.Expect("TNHTweaker -- Failed to load Fire Rate Down icon!")));
        }

        /// <summary>
        /// Loads the bepinex config file, and applys those settings
        /// </summary>
        private void LoadConfigFile()
        {
            TNHTweakerLogger.Log("TNHTweaker -- Getting config file", TNHTweakerLogger.LogType.File);

            allowLog = Source.Config.Bind("Debug",
                                    "EnableLogging",
                                    true,
                                    "Set to true to enable logging");

            printCharacters = Source.Config.Bind("Debug",
                                         "LogCharacterInfo",
                                         false,
                                         "Decide if should print all character info");

            logTNH = Source.Config.Bind("Debug",
                                    "LogTNH",
                                    false,
                                    "If true, general TNH information will be logged");

            logFileReads = Source.Config.Bind("Debug",
                                    "LogFileReads",
                                    false,
                                    "If true, reading from a file will log the reading process");

            TNHTweakerLogger.AllowLogging = allowLog.Value;
            TNHTweakerLogger.LogCharacter = printCharacters.Value;
            TNHTweakerLogger.LogTNH = logTNH.Value;
            TNHTweakerLogger.LogFile = logFileReads.Value;
        }

        /// <summary>
        /// Creates the main TNH Tweaker file folder
        /// </summary>
        private void SetupOutputDirectory()
        {
            OutputFilePath = Application.dataPath.Replace("/h3vr_Data", "/TNH_Tweaker");

            if (!Directory.Exists(OutputFilePath))
            {
                Directory.CreateDirectory(OutputFilePath);
            }
        }


        //////////////////////////////////
        //INITIALIZING TAKE AND HOLD SCENE
        //////////////////////////////////


        /// <summary>
        /// Performs initial setup of the TNH Scene when loaded
        /// </summary>
        /// <param name="___Categories"></param>
        /// <param name="___CharDatabase"></param>
        /// <param name="__instance"></param>
        /// <returns></returns>
        [HarmonyPatch(typeof(TNH_UIManager), "Start")] // Specify target method with HarmonyPatch attribute
        [HarmonyPrefix]
        public static bool InitTNH(List<TNH_UIManager.CharacterCategory> ___Categories, TNH_CharacterDatabase ___CharDatabase, TNH_UIManager __instance)
        {
            GM.TNHOptions.Char = TNH_Char.DD_ClassicLoudoutLouis;

            Text magazineCacheText = CreateMagazineCacheText(__instance);

            //Perform first time setup of all files
            if (!filesBuilt)
            {
                TNHTweakerLogger.Log("TNHTweaker -- Performing TNH Initialization", TNHTweakerLogger.LogType.General);

                //Load all of the default templates into our dictionaries
                TNHTweakerLogger.Log("TNHTweaker -- Adding default sosigs to template dictionary", TNHTweakerLogger.LogType.General);
                LoadDefaultSosigs();
                TNHTweakerLogger.Log("TNHTweaker -- Adding default characters to template dictionary", TNHTweakerLogger.LogType.General);
                LoadDefaultCharacters(___CharDatabase.Characters);

                LoadedTemplateManager.DefaultIconSprites = TNHTweakerUtils.GetAllIcons(LoadedTemplateManager.DefaultCharacters);

                TNHTweakerLogger.Log("TNHTweaker -- Delayed Init of default characters", TNHTweakerLogger.LogType.General);
                InitCharacters(LoadedTemplateManager.DefaultCharacters, false);

                TNHTweakerLogger.Log("TNHTweaker -- Delayed Init of custom characters", TNHTweakerLogger.LogType.General);
                InitCharacters(LoadedTemplateManager.CustomCharacters, true);

                TNHTweakerLogger.Log("TNHTweaker -- Delayed Init of custom sosigs", TNHTweakerLogger.LogType.General);
                InitSosigs(LoadedTemplateManager.CustomSosigs);

                //Create files relevant for character creation
                TNHTweakerLogger.Log("TNHTweaker -- Creating character creation files", TNHTweakerLogger.LogType.General);
                TNHTweakerUtils.CreateDefaultSosigTemplateFiles(LoadedTemplateManager.DefaultSosigs, OutputFilePath);
                TNHTweakerUtils.CreateDefaultCharacterFiles(LoadedTemplateManager.DefaultCharacters, OutputFilePath);
                TNHTweakerUtils.CreateIconIDFile(OutputFilePath, LoadedTemplateManager.DefaultIconSprites.Keys.ToList());
                TNHTweakerUtils.CreateObjectIDFile(OutputFilePath);
                TNHTweakerUtils.CreateSosigIDFile(OutputFilePath);
                TNHTweakerUtils.CreateJsonVaultFiles(OutputFilePath);

                SceneLoader sceneHotDog = FindObjectOfType<SceneLoader>();
                AnvilManager.Run(TNHTweakerUtils.LoadMagazineCacheAsync(OutputFilePath, magazineCacheText, sceneHotDog));
            }
            else
            {
                magazineCacheText.text = "CACHE BUILT";
            }

            //Setup the character panel to support more characters
            ExpandCharacterUI(__instance);
            
            //Load all characters into the UI
            foreach (TNH_CharacterDef character in LoadedTemplateManager.LoadedCharactersDict.Keys)
            {
                if (!___Categories[(int)character.Group].Characters.Contains(character.CharacterID))
                {
                    ___Categories[(int)character.Group].Characters.Add(character.CharacterID);
                    ___CharDatabase.Characters.Add(character);
                }
            }

            filesBuilt = true;
            return true;
        }

        /// <summary>
        /// Creates the additional text above the character select screen, and returns that text component
        /// </summary>
        /// <param name="manager"></param>
        /// <returns></returns>
        private static Text CreateMagazineCacheText(TNH_UIManager manager)
        {
            Text magazineCacheText = Instantiate(manager.SelectedCharacter_Title.gameObject, manager.SelectedCharacter_Title.transform.parent).GetComponent<Text>();
            magazineCacheText.transform.localPosition = new Vector3(0, 550, 0);
            magazineCacheText.transform.localScale = new Vector3(2, 2, 2);
            magazineCacheText.text = "EXAMPLE TEXT";

            return magazineCacheText;
        }


        /// <summary>
        /// Adds more space for characters to be displayed in the TNH menu
        /// </summary>
        /// <param name="manager"></param>
        private static void ExpandCharacterUI(TNH_UIManager manager)
        {
            //Add additional character buttons
            OptionsPanel_ButtonSet buttonSet = manager.LBL_CharacterName[1].transform.parent.GetComponent<OptionsPanel_ButtonSet>();
            List<FVRPointableButton> buttonList = new List<FVRPointableButton>(buttonSet.ButtonsInSet);
            for (int i = 0; i < 6; i++)
            {
                Text newCharacterLabel = Instantiate(manager.LBL_CharacterName[1].gameObject, manager.LBL_CharacterName[1].transform.parent).GetComponent<Text>();
                Button newButton = newCharacterLabel.gameObject.GetComponent<Button>();

                int buttonIndex = 6 + i;

                newButton.onClick = new Button.ButtonClickedEvent();
                newButton.onClick.AddListener(() => { manager.SetSelectedCharacter(buttonIndex); });
                newButton.onClick.AddListener(() => { buttonSet.SetSelectedButton(buttonIndex); });

                manager.LBL_CharacterName.Add(newCharacterLabel);
                buttonList.Add(newCharacterLabel.gameObject.GetComponent<FVRPointableButton>());
            }
            buttonSet.ButtonsInSet = buttonList.ToArray();

            //Adjust buttons to be tighter together
            float prevY = manager.LBL_CharacterName[0].transform.localPosition.y;
            for (int i = 1; i < manager.LBL_CharacterName.Count; i++)
            {
                prevY = prevY - 35f;
                manager.LBL_CharacterName[i].transform.localPosition = new Vector3(250, prevY, 0);
            }
        }

        /// <summary>
        /// Loads all default sosigs into the template manager
        /// </summary>
        private static void LoadDefaultSosigs()
        {
            foreach (SosigEnemyTemplate sosig in ManagerSingleton<IM>.Instance.odicSosigObjsByID.Values)
            {
                LoadedTemplateManager.AddSosigTemplate(sosig);
            }
        }

        /// <summary>
        /// Loads all default characters into the template manager
        /// </summary>
        /// <param name="characters">A list of TNH characters</param>
        private static void LoadDefaultCharacters(List<TNH_CharacterDef> characters)
        {
            foreach (TNH_CharacterDef character in characters)
            {
                LoadedTemplateManager.AddCharacterTemplate(character);
            }
        }

        /// <summary>
        /// Performs a delayed init on the sent list of custom characters, and removes any characters that failed to init
        /// </summary>
        /// <param name="characters"></param>
        /// <param name="isCustom"></param>
        private static void InitCharacters(List<CustomCharacter> characters, bool isCustom)
        {
            for (int i = 0; i < characters.Count; i++)
            {
                CustomCharacter character = characters[i];

                try
                {
                    character.DelayedInit(isCustom);
                }
                catch (Exception e)
                {
                    TNHTweakerLogger.LogError("TNHTweaker -- Failed to load character: " + character.DisplayName + ". Error Output:\n" + e.ToString());
                    characters.RemoveAt(i);
                    LoadedTemplateManager.LoadedCharactersDict.Remove(character.GetCharacter());
                    i -= 1;
                }
            }
        }

        /// <summary>
        /// Performs a delayed init on the sent list of sosigs. If a sosig fails to init, any character using that sosig will be removed
        /// </summary>
        /// <param name="sosigs"></param>
        private static void InitSosigs(List<SosigTemplate> sosigs)
        {
            for (int i = 0; i < sosigs.Count; i++)
            {
                SosigTemplate sosig = sosigs[i];

                try
                {
                    sosig.DelayedInit();
                }
                catch (Exception e)
                {
                    TNHTweakerLogger.LogError("TNHTweaker -- Failed to load sosig: " + sosig.DisplayName + ". Error Output:\n" + e.ToString());

                    //Find any characters that use this sosig, and remove them
                    for (int j = 0; j < LoadedTemplateManager.LoadedCharactersDict.Values.Count; j++)
                    {
                        //This is probably monsterously inefficient, but if you're at this point you're already fucked :)
                        KeyValuePair<TNH_CharacterDef, CustomCharacter> value_pair = LoadedTemplateManager.LoadedCharactersDict.ToList()[j];

                        if (value_pair.Value.CharacterUsesSosig(sosig.SosigEnemyID))
                        {
                            TNHTweakerLogger.LogError("TNHTweaker -- Removing character that used removed sosig: " + value_pair.Value.DisplayName);
                            LoadedTemplateManager.LoadedCharactersDict.Remove(value_pair.Key);
                            j -= 1;
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// Generates a file which shows every item in the equipment pools of the character
        /// </summary>
        /// <param name="___m_objectTableDics"></param>
        [HarmonyPatch(typeof(TNH_Manager), "InitTables")] // Specify target method with HarmonyPatch attribute
        [HarmonyPostfix]
        public static void PrintGenerateTables(Dictionary<ObjectTableDef, ObjectTable> ___m_objectTableDics)
        {
            try
            {
                string path = OutputFilePath + "/pool_contents.txt";

                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                // Create a new file     
                using (StreamWriter sw = File.CreateText(path))
                {
                    foreach (KeyValuePair<ObjectTableDef, ObjectTable> pool in ___m_objectTableDics)
                    {
                        sw.WriteLine("Pool: " + pool.Key.Icon.name);
                        foreach(FVRObject obj in pool.Value.Objs)
                        {
                            if(obj == null)
                            {
                                TNHTweakerLogger.Log("TNHTWEAKER -- Null object in character table", TNHTweakerLogger.LogType.Character);
                                continue;
                            }
                            sw.WriteLine("-" + obj.ItemID);
                        }
                        sw.WriteLine("");
                    }
                }
            }

            catch (Exception ex)
            {
                //Debug.LogError(ex.ToString());
            }
        }


        /////////////////////////////
        //PATCHES FOR PATROL SPAWNING
        /////////////////////////////


        /// <summary>
        /// Finds an index in the patrols list which can spawn, preventing bosses that have already spawned from spawning again
        /// </summary>
        /// <param name="patrols">List of patrols that can spawn</param>
        /// <returns>Returns -1 if no valid index is found, otherwise returns a random index for a patrol </returns>
        private static int GetValidPatrolIndex(List<TNH_PatrolChallenge.Patrol> patrols)
        {
            int index = UnityEngine.Random.Range(0, patrols.Count);
            int attempts = 0;

            while(spawnedBossIndexes.Contains(index) && attempts < patrols.Count)
            {
                attempts += 1;
                index += 1;
                if (index >= patrols.Count) index = 0;
            }

            if (spawnedBossIndexes.Contains(index)) return -1;

            return index;
        }


        /// <summary>
        /// Decides the spawning location and patrol pathing for sosig patrols, and then spawns the patrol
        /// </summary>
        /// <param name="P"></param>
        /// <param name="curStandardIndex"></param>
        /// <param name="excludeHoldIndex"></param>
        /// <param name="isStart"></param>
        /// <param name="__instance"></param>
        /// <param name="___m_curLevel"></param>
        /// <param name="___m_patrolSquads"></param>
        /// <param name="___m_timeTilPatrolCanSpawn"></param>
        /// <returns></returns>
        [HarmonyPatch(typeof(TNH_Manager), "GenerateValidPatrol")] // Specify target method with HarmonyPatch attribute
        [HarmonyPrefix]
        public static bool GenerateValidPatrolReplacement(TNH_PatrolChallenge P, int curStandardIndex, int excludeHoldIndex, bool isStart, TNH_Manager __instance, TNH_Progression.Level ___m_curLevel, List<TNH_Manager.SosigPatrolSquad> ___m_patrolSquads, ref float ___m_timeTilPatrolCanSpawn)
        {
            TNHTweakerLogger.Log("TNHTWEAKER -- Generating a patrol -- There are currently " + ___m_patrolSquads.Count + " patrols active", TNHTweakerLogger.LogType.TNH);

            if (P.Patrols.Count < 1) return false;

            //Get a valid patrol index, and exit if there are no valid patrols
            int patrolIndex = GetValidPatrolIndex(P.Patrols);
            if(patrolIndex == -1)
            {
                TNHTweakerLogger.Log("TNHTWEAKER -- No valid patrols can spawn", TNHTweakerLogger.LogType.TNH);
                ___m_timeTilPatrolCanSpawn = 999;
                return false;
            }

            TNHTweakerLogger.Log("TNHTWEAKER -- Valid patrol found", TNHTweakerLogger.LogType.TNH);

            TNH_PatrolChallenge.Patrol patrol = P.Patrols[patrolIndex];

            List<int> validLocations = new List<int>();
            float minDist = __instance.TAHReticle.Range * 1.2f;

            //Get a safe starting point for the patrol to spawn
            TNH_SafePositionMatrix.PositionEntry startingEntry;
            if (isStart) startingEntry = __instance.SafePosMatrix.Entries_SupplyPoints[curStandardIndex];
            else startingEntry = __instance.SafePosMatrix.Entries_HoldPoints[curStandardIndex];


            for(int i = 0; i < startingEntry.SafePositions_HoldPoints.Count; i++)
            {
                if(i != excludeHoldIndex && startingEntry.SafePositions_HoldPoints[i])
                {
                    float playerDist = Vector3.Distance(GM.CurrentPlayerBody.transform.position, __instance.HoldPoints[i].transform.position);
                    if(playerDist > minDist)
                    {
                        validLocations.Add(i);
                    }
                }
            }

            if (validLocations.Count < 1) return false;
            validLocations.Shuffle();

            TNH_Manager.SosigPatrolSquad squad = GeneratePatrol(validLocations[0], __instance, ___m_curLevel, patrol, patrolIndex);

            if(__instance.EquipmentMode == TNHSetting_EquipmentMode.Spawnlocking)
            {
                ___m_timeTilPatrolCanSpawn = patrol.TimeTilRegen;
            }
            else
            {
                ___m_timeTilPatrolCanSpawn = patrol.TimeTilRegen_LimitedAmmo;
            }

            ___m_patrolSquads.Add(squad);

            return false;
        }

        
        /// <summary>
        /// Spawns a patrol at the desire patrol point
        /// </summary>
        /// <param name="HoldPointStart"></param>
        /// <param name="instance"></param>
        /// <param name="level"></param>
        /// <param name="patrol"></param>
        /// <param name="patrolIndex"></param>
        /// <returns></returns>
        public static TNH_Manager.SosigPatrolSquad GeneratePatrol(int HoldPointStart, TNH_Manager instance, TNH_Progression.Level level, TNH_PatrolChallenge.Patrol patrol, int patrolIndex)
        {
            TNH_Manager.SosigPatrolSquad squad = new TNH_Manager.SosigPatrolSquad();

            squad.PatrolPoints = new List<Vector3>();
            foreach(TNH_HoldPoint holdPoint in instance.HoldPoints)
            {
                squad.PatrolPoints.Add(holdPoint.SpawnPoints_Sosigs_Defense.GetRandom<Transform>().position);
            }

            Vector3 startingPoint = squad.PatrolPoints[HoldPointStart];
            squad.PatrolPoints.RemoveAt(HoldPointStart);
            squad.PatrolPoints.Insert(0, startingPoint);

            int PatrolSize = Mathf.Clamp(patrol.PatrolSize, 0, instance.HoldPoints[HoldPointStart].SpawnPoints_Sosigs_Defense.Count);

            CustomCharacter character = LoadedTemplateManager.LoadedCharactersDict[instance.C];
            Level currLevel = character.GetCurrentLevel(level);
            Patrol currPatrol = currLevel.GetPatrol(patrol);

            TNHTweakerLogger.Log("TNHTWEAKER -- Is patrol a boss?: " + currPatrol.IsBoss, TNHTweakerLogger.LogType.TNH);

            for (int i = 0; i < PatrolSize; i++)
            {
                SosigEnemyTemplate template;
                bool allowAllWeapons;

                //If this is a boss, then we can only spawn it once, so add it to the list of spawned bosses
                if (currPatrol.IsBoss)
                {
                    spawnedBossIndexes.Add(patrolIndex);
                }

                //Select a sosig template from the custom character patrol
                if (i == 0)
                {
                    template = ManagerSingleton<IM>.Instance.odicSosigObjsByID[(SosigEnemyID)LoadedTemplateManager.SosigIDDict[currPatrol.LeaderType]];
                    allowAllWeapons = true;
                }

                else
                {
                    template = ManagerSingleton<IM>.Instance.odicSosigObjsByID[(SosigEnemyID)LoadedTemplateManager.SosigIDDict[currPatrol.EnemyType.GetRandom<string>()]];
                    allowAllWeapons = false;
                }


                SosigTemplate customTemplate = LoadedTemplateManager.LoadedSosigsDict[template];
                FVRObject droppedObject = instance.Prefab_HealthPickupMinor;

                //If squad is set to swarm, the first point they path to should be the players current position
                Sosig sosig;
                if (currPatrol.SwarmPlayer)
                {
                    squad.PatrolPoints[0] = GM.CurrentPlayerBody.transform.position;
                    sosig = SpawnEnemy(customTemplate, character, instance.HoldPoints[HoldPointStart].SpawnPoints_Sosigs_Defense[i], instance.AI_Difficulty, currPatrol.IFFUsed, true, squad.PatrolPoints[0], allowAllWeapons);
                    sosig.SetAssaultSpeed(currPatrol.AssualtSpeed);
                }
                else
                {
                    sosig = SpawnEnemy(customTemplate, character, instance.HoldPoints[HoldPointStart].SpawnPoints_Sosigs_Defense[i], instance.AI_Difficulty, currPatrol.IFFUsed, true, squad.PatrolPoints[0], allowAllWeapons);
                    sosig.SetAssaultSpeed(currPatrol.AssualtSpeed);
                }

                //Handle patrols dropping health
                if(i == 0 && UnityEngine.Random.value < currPatrol.DropChance)
                {
                    sosig.Links[1].RegisterSpawnOnDestroy(droppedObject);
                }

                squad.Squad.Add(sosig);
            }

            return squad;
        }



        ///////////////////////////////////////////
        //PATCHES FOR SUPPLY POINTS AND TAKE POINTS
        ///////////////////////////////////////////


        [HarmonyPatch(typeof(TNH_Manager), "SetPhase_Take")] // Specify target method with HarmonyPatch attribute
        [HarmonyPrefix]
        public static bool SetPhase_Take_Replacement(
            TNH_Manager __instance,
            int ___m_level,
            TNH_Progression.Level ___m_curLevel,
            TNH_PointSequence ___m_curPointSequence,
            ref int ___m_curHoldIndex,
            ref TNH_HoldPoint ___m_curHoldPoint)
        {
            spawnedBossIndexes.Clear();
            preventOutfitFunctionality = LoadedTemplateManager.LoadedCharactersDict[__instance.C].ForceDisableOutfitFunctionality;

            TNHTweakerLogger.Log("Makarov rounds: " + IM.OD["Makarov"].CompatibleSingleRounds.Count, TNHTweakerLogger.LogType.General);

            //Clear the TNH radar
            if (__instance.RadarMode == TNHModifier_RadarMode.Standard)
            {
                __instance.TAHReticle.GetComponent<AIEntity>().LM_VisualOcclusionCheck = __instance.ReticleMask_Take;
            }
            else if(__instance.RadarMode == TNHModifier_RadarMode.Omnipresent)
            {
                __instance.TAHReticle.GetComponent<AIEntity>().LM_VisualOcclusionCheck = __instance.ReticleMask_Hold;
            }

            __instance.TAHReticle.DeRegisterTrackedType(TAH_ReticleContact.ContactType.Hold);
            __instance.TAHReticle.DeRegisterTrackedType(TAH_ReticleContact.ContactType.Supply);


            //Get the next hold point and configure it
            ___m_curHoldIndex = GetNextHoldPointIndex(__instance, ___m_curPointSequence, ___m_level, ___m_curHoldIndex);
            ___m_curHoldPoint = __instance.HoldPoints[___m_curHoldIndex];
            ___m_curHoldPoint.ConfigureAsSystemNode(___m_curLevel.TakeChallenge, ___m_curLevel.HoldChallenge, ___m_curLevel.NumOverrideTokensForHold);
            
            __instance.TAHReticle.RegisterTrackedObject(___m_curHoldPoint.SpawnPoint_SystemNode, TAH_ReticleContact.ContactType.Hold);

            CustomCharacter character = LoadedTemplateManager.LoadedCharactersDict[__instance.C];
            Level level = character.GetCurrentLevel(___m_curLevel);

            
            //Generate all of the supply points for this level
            List<int> supplyPointsIndexes = GetNextSupplyPointIndexes(__instance, ___m_curPointSequence, ___m_level, ___m_curHoldIndex);
            int numSupplyPoints = UnityEngine.Random.Range(level.MinSupplyPoints, level.MaxSupplyPoints + 1);
            numSupplyPoints = Mathf.Clamp(numSupplyPoints, 0, supplyPointsIndexes.Count);
            level.PossiblePanelTypes.Shuffle();

            TNHTweakerLogger.Log("TNHTWEAKER -- Panel types for this hold:", TNHTweakerLogger.LogType.TNH);
            level.PossiblePanelTypes.ForEach(o => TNHTweakerLogger.Log(o.ToString(), TNHTweakerLogger.LogType.TNH));

            TNHTweakerLogger.Log("TNHTWEAKER -- Spawning " + numSupplyPoints + " supply points", TNHTweakerLogger.LogType.TNH);
            for (int i = 0; i < numSupplyPoints; i++)
            {
                TNH_SupplyPoint supplyPoint = __instance.SupplyPoints[supplyPointsIndexes[i]];
                ConfigureSupplyPoint(supplyPoint, level, i);
                TAH_ReticleContact contact = __instance.TAHReticle.RegisterTrackedObject(supplyPoint.SpawnPoint_PlayerSpawn, TAH_ReticleContact.ContactType.Supply);
                supplyPoint.SetContact(contact);
            }

            if(__instance.BGAudioMode == TNH_BGAudioMode.Default)
            {
                __instance.FMODController.SwitchTo(0, 2f, false, false);
            }

            return false;
        }

        public static void ConfigureSupplyPoint(TNH_SupplyPoint supplyPoint, Level level, int supplyIndex)
        {
            TNHTweakerLogger.Log("TNHTWEAKER -- Configuring supply point : " + supplyIndex, TNHTweakerLogger.LogType.TNH);

            supplyPoint.T = level.SupplyChallenge.GetTakeChallenge();

            Traverse pointTraverse = Traverse.Create(supplyPoint);

            SpawnSupplyGroup(supplyPoint, level);

            SpawnSupplyTurrets(supplyPoint, level);

            int numConstructors = UnityEngine.Random.Range(level.MinConstructors, level.MaxConstructors + 1);

            SpawnSupplyConstructor(supplyPoint, numConstructors);

            SpawnSecondarySupplyPanel(supplyPoint, level, numConstructors, supplyIndex);

            SpawnSupplyBoxes(supplyPoint, level);

            pointTraverse.Field("m_hasBeenVisited").SetValue(false);
        }


        public static void SpawnSupplyConstructor(TNH_SupplyPoint point, int toSpawn)
        {
            TNHTweakerLogger.Log("TNHTWEAKER -- Spawning constructor panel", TNHTweakerLogger.LogType.TNH);

            point.SpawnPoints_Panels.Shuffle();
            
            for(int i = 0; i < toSpawn && i < point.SpawnPoints_Panels.Count; i++)
            {
                GameObject constructor = point.M.SpawnObjectConstructor(point.SpawnPoints_Panels[i]);
                SpawnedConstructors.Add(constructor);
            }
        }
        
        public static void SpawnSecondarySupplyPanel(TNH_SupplyPoint point, Level level, int startingPanelIndex, int supplyIndex)
        {
            TNHTweakerLogger.Log("TNHTWEAKER -- Spawning secondary panels", TNHTweakerLogger.LogType.TNH);

            PanelType panelType;
            List<PanelType> panelTypes = new List<PanelType>(level.PossiblePanelTypes);

            if (point.M.EquipmentMode != TNHSetting_EquipmentMode.LimitedAmmo)
            {
                TNHTweakerLogger.Log("TNHTWEAKER -- Removing mag duplicator since we are on limited ammo mode", TNHTweakerLogger.LogType.TNH);
                panelTypes.Remove(PanelType.MagDuplicator);
            }

            int numPanels = UnityEngine.Random.Range(level.MinPanels, level.MaxPanels + 1);

            for (int i = startingPanelIndex; i < startingPanelIndex + numPanels && i < point.SpawnPoints_Panels.Count && panelTypes.Count > 0; i++)
            {
                TNHTweakerLogger.Log("TNHTWEAKER -- Panel index : " + i, TNHTweakerLogger.LogType.TNH);

                //If this is the first panel, we should ensure that it is an ammo resupply
                if (panelTypes.Contains(PanelType.AmmoReloader) && point.M.EquipmentMode == TNHSetting_EquipmentMode.LimitedAmmo && i == startingPanelIndex && supplyIndex == 0)
                {
                    TNHTweakerLogger.Log("TNHTWEAKER -- First supply and first panel on limited ammo, forcing ammo reloader to spawn", TNHTweakerLogger.LogType.TNH);
                    panelType = PanelType.AmmoReloader;
                    panelTypes.Remove(PanelType.AmmoReloader);
                }

                //Otherwise we just select a random panel from valid panels
                else
                {
                    if (supplyIndex >= panelTypes.Count) supplyIndex = 0;
                    panelType = panelTypes[supplyIndex];
                    supplyIndex += 1;

                    TNHTweakerLogger.Log("TNHTWEAKER -- Panel type selected : " + panelType, TNHTweakerLogger.LogType.TNH);
                }

                GameObject panel = null;

                if (panelType == PanelType.AmmoReloader)
                {
                    panel = point.M.SpawnAmmoReloader(point.SpawnPoints_Panels[i]);
                }

                else if (panelType == PanelType.MagDuplicator)
                {
                    panel = point.M.SpawnMagDuplicator(point.SpawnPoints_Panels[i]);
                }

                else if (panelType == PanelType.Recycler)
                {
                    panel = point.M.SpawnGunRecycler(point.SpawnPoints_Panels[i]);
                }

                else if (panelType == PanelType.MagUpgrader)
                {
                    panel = point.M.SpawnMagDuplicator(point.SpawnPoints_Panels[i]);
                    panel.AddComponent(typeof(MagUpgrader));
                }

                else if (panelType == PanelType.AddFullAuto)
                {
                    panel = point.M.SpawnMagDuplicator(point.SpawnPoints_Panels[i]);
                    panel.AddComponent(typeof(FullAutoEnabler));
                }

                else if (panelType == PanelType.FireRateUp || panelType == PanelType.FireRateDown)
                {
                    panel = point.M.SpawnMagDuplicator(point.SpawnPoints_Panels[i]);
                    FireRateModifier component = (FireRateModifier)panel.AddComponent(typeof(FireRateModifier));
                    component.Init(panelType);
                }

                else if (panelType == PanelType.MagPurchase)
                {
                    panel = point.M.SpawnMagDuplicator(point.SpawnPoints_Panels[i]);
                    panel.AddComponent(typeof(MagPurchaser));
                }

                else if (panelType == PanelType.AmmoPurchase)
                {
                    panel = point.M.SpawnMagDuplicator(point.SpawnPoints_Panels[i]);
                    panel.AddComponent(typeof(AmmoPurchaser));
                }

                //If we spawned a panel, add it to the global list
                if (panel != null)
                {
                    TNHTweakerLogger.Log("TNHTWEAKER -- Panel spawned successfully", TNHTweakerLogger.LogType.TNH);
                    SpawnedPanels.Add(panel);
                }
                else
                {
                    TNHTweakerLogger.LogWarning("TNHTWEAKER -- Failed to spawn secondary panel!");
                }
            }
        }

        public static void SpawnSupplyGroup(TNH_SupplyPoint point, Level level)
        {
            point.SpawnPoints_Sosigs_Defense.Shuffle<Transform>();

            Traverse pointTraverse = Traverse.Create(point);

            for (int i = 0; i < level.SupplyChallenge.NumGuards && i < point.SpawnPoints_Sosigs_Defense.Count; i++)
            {
                Transform transform = point.SpawnPoints_Sosigs_Defense[i];
                SosigEnemyTemplate template = ManagerSingleton<IM>.Instance.odicSosigObjsByID[level.SupplyChallenge.GetTakeChallenge().GID];
                SosigTemplate customTemplate = LoadedTemplateManager.LoadedSosigsDict[template];

                Sosig enemy = SpawnEnemy(customTemplate, LoadedTemplateManager.LoadedCharactersDict[point.M.C], transform, point.M.AI_Difficulty, level.SupplyChallenge.IFFUsed, false, transform.position, true);

                pointTraverse.Field("m_activeSosigs").Method("Add", enemy).GetValue();
            }
        }


        public static void SpawnSupplyTurrets(TNH_SupplyPoint point, Level level)
        {
            point.SpawnPoints_Turrets.Shuffle<Transform>();
            FVRObject turretPrefab = point.M.GetTurretPrefab(level.SupplyChallenge.TurretType);

            Traverse pointTraverse = Traverse.Create(point);

            for (int i = 0; i < level.SupplyChallenge.NumTurrets && i < point.SpawnPoints_Turrets.Count; i++)
            {
                Vector3 pos = point.SpawnPoints_Turrets[i].position + Vector3.up * 0.25f;
                AutoMeater turret = Instantiate<GameObject>(turretPrefab.GetGameObject(), pos, point.SpawnPoints_Turrets[i].rotation).GetComponent<AutoMeater>();
                pointTraverse.Field("m_activeTurrets").Method("Add", turret).GetValue();
            }

        }


        public static void SpawnSupplyBoxes(TNH_SupplyPoint point, Level level)
        {
            List<GameObject> boxes = (List<GameObject>)Traverse.Create(point).Field("m_spawnBoxes").GetValue();

            point.SpawnPoints_Boxes.Shuffle();

            int boxesToSpawn = UnityEngine.Random.Range(level.MinBoxesSpawned, level.MaxBoxesSpawned + 1);

            TNHTweakerLogger.Log("TNHTWEAKER -- Going to spawn " + boxesToSpawn + " boxes at this point -- Min (" + level.MinBoxesSpawned + "), Max (" + level.MaxBoxesSpawned + ")", TNHTweakerLogger.LogType.TNH);

            for (int i = 0; i < boxesToSpawn; i++)
            {
                Transform spawnTransform = point.SpawnPoints_Boxes[UnityEngine.Random.Range(0, point.SpawnPoints_Boxes.Count)];
                Vector3 position = spawnTransform.position + Vector3.up * 0.1f + Vector3.right * UnityEngine.Random.Range(-0.5f, 0.5f) + Vector3.forward * UnityEngine.Random.Range(-0.5f, 0.5f);
                Quaternion rotation = Quaternion.Slerp(spawnTransform.rotation, UnityEngine.Random.rotation, 0.1f);
                GameObject box = Instantiate(point.M.Prefabs_ShatterableCrates[UnityEngine.Random.Range(0, point.M.Prefabs_ShatterableCrates.Count)], position, rotation);
                boxes.Add(box);
            }

            int tokensSpawned = 0;

            foreach (GameObject boxObj in boxes)
            {
                if (tokensSpawned < level.MinTokensPerSupply)
                {
                    boxObj.GetComponent<TNH_ShatterableCrate>().SetHoldingToken(point.M);
                    tokensSpawned += 1;
                }

                else if (tokensSpawned < level.MaxTokensPerSupply && UnityEngine.Random.value < level.BoxTokenChance)
                {
                    boxObj.GetComponent<TNH_ShatterableCrate>().SetHoldingToken(point.M);
                    tokensSpawned += 1;
                }

                else if (UnityEngine.Random.value < level.BoxHealthChance)
                {
                    boxObj.GetComponent<TNH_ShatterableCrate>().SetHoldingHealth(point.M);
                }
            }
        }


        public static int GetNextHoldPointIndex(TNH_Manager M, TNH_PointSequence pointSequence, int currLevel, int currHoldIndex)
        {
            int index;

            //If we havn't gone through all the hold points, we just select the next one we havn't been to
            if (currLevel < pointSequence.HoldPoints.Count)
            {
                index = pointSequence.HoldPoints[currLevel];
            }

            //If we have been to all the points, then we just select a random safe one
            else
            {
                List<int> pointIndexes = new List<int>();
                for (int i = 0; i < M.SafePosMatrix.Entries_HoldPoints[currHoldIndex].SafePositions_HoldPoints.Count; i++)
                {
                    if (i != currHoldIndex && M.SafePosMatrix.Entries_HoldPoints[currHoldIndex].SafePositions_HoldPoints[i])
                    {
                        pointIndexes.Add(i);
                    }
                }

                index = pointIndexes.GetRandom();
            }

            return index;
        }


        public static List<int> GetNextSupplyPointIndexes(TNH_Manager M, TNH_PointSequence pointSequence, int currLevel, int currHoldIndex)
        {
            List<int> indexes = new List<int>();

            if(currLevel == 0)
            {
                for(int i = 0; i < M.SafePosMatrix.Entries_SupplyPoints[pointSequence.StartSupplyPointIndex].SafePositions_SupplyPoints.Count; i++)
                {
                    if (M.SafePosMatrix.Entries_SupplyPoints[pointSequence.StartSupplyPointIndex].SafePositions_SupplyPoints[i])
                    {
                        indexes.Add(i);
                    }
                }
            }
            else
            {
                for(int i = 0; i < M.SafePosMatrix.Entries_HoldPoints[currHoldIndex].SafePositions_SupplyPoints.Count; i++)
                {
                    if (M.SafePosMatrix.Entries_HoldPoints[currHoldIndex].SafePositions_SupplyPoints[i])
                    {
                        indexes.Add(i);
                    }
                }
            }

            indexes.Shuffle();

            return indexes;
        }


        [HarmonyPatch(typeof(TNH_HoldPoint), "SpawnTakeEnemyGroup")] // Specify target method with HarmonyPatch attribute
        [HarmonyPrefix]
        public static bool SpawnTakeGroupReplacement(List<Transform> ___SpawnPoints_Sosigs_Defense, TNH_TakeChallenge ___T, TNH_Manager ___M, List<Sosig> ___m_activeSosigs)
        {
            ___SpawnPoints_Sosigs_Defense.Shuffle<Transform>();

            for(int i = 0; i < ___T.NumGuards && i < ___SpawnPoints_Sosigs_Defense.Count; i++)
            {
                Transform transform = ___SpawnPoints_Sosigs_Defense[i];
                //Debug.Log("Take challenge sosig ID : " + ___T.GID);
                SosigEnemyTemplate template = ManagerSingleton<IM>.Instance.odicSosigObjsByID[___T.GID];
                SosigTemplate customTemplate = LoadedTemplateManager.LoadedSosigsDict[template];

                Sosig enemy = SpawnEnemy(customTemplate, LoadedTemplateManager.LoadedCharactersDict[___M.C], transform, ___M.AI_Difficulty, ___T.IFFUsed, false, transform.position, true);

                ___m_activeSosigs.Add(enemy);
            }

            return false;
        }



        [HarmonyPatch(typeof(TNH_HoldPoint), "SpawnTurrets")] // Specify target method with HarmonyPatch attribute
        [HarmonyPrefix]
        public static bool SpawnTurretsReplacement(List<Transform> ___SpawnPoints_Turrets, TNH_TakeChallenge ___T, TNH_Manager ___M, List<AutoMeater> ___m_activeTurrets)
        {
            ___SpawnPoints_Turrets.Shuffle<Transform>();
            FVRObject turretPrefab = ___M.GetTurretPrefab(___T.TurretType);

            for (int i = 0; i < ___T.NumTurrets && i < ___SpawnPoints_Turrets.Count; i++)
            {
                Vector3 pos = ___SpawnPoints_Turrets[i].position + Vector3.up * 0.25f;
                AutoMeater turret = Instantiate<GameObject>(turretPrefab.GetGameObject(), pos, ___SpawnPoints_Turrets[i].rotation).GetComponent<AutoMeater>();
                ___m_activeTurrets.Add(turret);
            }

            return false;
        }



        ///////////////////////////////
        //PATCHES FOR DURING HOLD POINT
        ///////////////////////////////



        [HarmonyPatch(typeof(TNH_HoldPoint), "IdentifyEncryption")] // Specify target method with HarmonyPatch attribute
        [HarmonyPrefix]
        public static bool IdentifyEncryptionReplacement(TNH_HoldPoint __instance, TNH_HoldChallenge.Phase ___m_curPhase)
        {
            CustomCharacter character = LoadedTemplateManager.LoadedCharactersDict[__instance.M.C];
            Phase currentPhase = character.GetCurrentPhase(___m_curPhase);
            Traverse holdTraverse = Traverse.Create(__instance);

            //If we shouldnt spawn any targets, we exit out early
            if ((currentPhase.MaxTargets < 1 && __instance.M.EquipmentMode == TNHSetting_EquipmentMode.Spawnlocking) ||
                (currentPhase.MaxTargetsLimited < 1 && __instance.M.EquipmentMode == TNHSetting_EquipmentMode.LimitedAmmo))
            {
                holdTraverse.Method("CompletePhase").GetValue();
                return false;
            }

            holdTraverse.Field("m_state").SetValue(TNH_HoldPoint.HoldState.Hacking);
            holdTraverse.Field("m_tickDownToFailure").SetValue(120f);

            __instance.M.EnqueueEncryptionLine(currentPhase.Encryptions[0]);
            
            holdTraverse.Method("DeleteAllActiveWarpIns").GetValue();
            SpawnEncryptionReplacement(__instance, currentPhase);
            holdTraverse.Field("m_systemNode").Method("SetNodeMode", TNH_HoldPointSystemNode.SystemNodeMode.Indentified).GetValue();

            return false;
        }


        public static void SpawnEncryptionReplacement(TNH_HoldPoint holdPoint, Phase currentPhase)
        {
            int numTargets;
            if (holdPoint.M.EquipmentMode == TNHSetting_EquipmentMode.LimitedAmmo)
            {
                numTargets = UnityEngine.Random.Range(currentPhase.MinTargetsLimited, currentPhase.MaxTargetsLimited + 1);
            }
            else
            {
                numTargets = UnityEngine.Random.Range(currentPhase.MinTargets, currentPhase.MaxTargets + 1);
            }

            List<FVRObject> encryptions = currentPhase.Encryptions.Select(o => holdPoint.M.GetEncryptionPrefab(o)).ToList();
            for(int i = 0; i < numTargets && i < holdPoint.SpawnPoints_Targets.Count; i++)
            {
                GameObject gameObject = Instantiate(encryptions[i % encryptions.Count].GetGameObject(), holdPoint.SpawnPoints_Targets[i].position, holdPoint.SpawnPoints_Targets[i].rotation);
                TNH_EncryptionTarget encryption = gameObject.GetComponent<TNH_EncryptionTarget>();
                encryption.SetHoldPoint(holdPoint);
                holdPoint.RegisterNewTarget(encryption);
            }
        }

        public static void SpawnGrenades(List<TNH_HoldPoint.AttackVector> AttackVectors, TNH_Manager M, int m_phaseIndex)
        {
            CustomCharacter character = LoadedTemplateManager.LoadedCharactersDict[M.C];
            Level currLevel = character.GetCurrentLevel((TNH_Progression.Level)Traverse.Create(M).Field("m_curLevel").GetValue());
            Phase currPhase = currLevel.HoldPhases[m_phaseIndex];

            float grenadeChance = currPhase.GrenadeChance;
            string grenadeType = currPhase.GrenadeType;

            if (grenadeChance >= UnityEngine.Random.Range(0f, 1f))
            {
                TNHTweakerLogger.Log("TNHTWEAKER -- Throwing grenade ", TNHTweakerLogger.LogType.TNH);

                //Get a random grenade vector to spawn a grenade at
                TNH_HoldPoint.AttackVector randAttackVector = AttackVectors[UnityEngine.Random.Range(0, AttackVectors.Count)];

                //Instantiate the grenade object
                GameObject grenadeObject = Instantiate(IM.OD[grenadeType].GetGameObject(), randAttackVector.GrenadeVector.position, randAttackVector.GrenadeVector.rotation);

                //Give the grenade an initial velocity based on the grenade vector
                grenadeObject.GetComponent<Rigidbody>().velocity = 15 * randAttackVector.GrenadeVector.forward;
                grenadeObject.GetComponent<SosigWeapon>().FuseGrenade();
            }
        }



        public static void SpawnHoldEnemyGroup(TNH_HoldChallenge.Phase curPhase, int phaseIndex, List<TNH_HoldPoint.AttackVector> AttackVectors, List<Transform> SpawnPoints_Turrets, List<Sosig> ActiveSosigs, TNH_Manager M, ref bool isFirstWave)
        {
            TNHTweakerLogger.Log("TNHTWEAKER -- Spawning enemy wave", TNHTweakerLogger.LogType.TNH);

            //TODO add custom property form MinDirections
            int numAttackVectors = UnityEngine.Random.Range(1, curPhase.MaxDirections + 1);
            numAttackVectors = Mathf.Clamp(numAttackVectors, 1, AttackVectors.Count);

            //Get the custom character data
            CustomCharacter character = LoadedTemplateManager.LoadedCharactersDict[M.C];
            Level currLevel = character.GetCurrentLevel((TNH_Progression.Level)Traverse.Create(M).Field("m_curLevel").GetValue());
            Phase currPhase = currLevel.HoldPhases[phaseIndex];

            //Set first enemy to be spawned as leader
            SosigEnemyTemplate enemyTemplate = ManagerSingleton<IM>.Instance.odicSosigObjsByID[(SosigEnemyID)LoadedTemplateManager.SosigIDDict[currPhase.LeaderType]];
            int enemiesToSpawn = UnityEngine.Random.Range(curPhase.MinEnemies, curPhase.MaxEnemies + 1);

            int sosigsSpawned = 0;
            int vectorSpawnPoint = 0;
            Vector3 targetVector;
            int vectorIndex = 0;
            while (sosigsSpawned < enemiesToSpawn)
            {
                TNHTweakerLogger.Log("TNHTWEAKER -- Spawning at attack vector: " + vectorIndex, TNHTweakerLogger.LogType.TNH);

                if (AttackVectors[vectorIndex].SpawnPoints_Sosigs_Attack.Count <= vectorSpawnPoint) break;

                //Set the sosigs target position
                if (currPhase.SwarmPlayer)
                {
                    targetVector = GM.CurrentPlayerBody.TorsoTransform.position;
                }
                else
                {
                    targetVector = SpawnPoints_Turrets[UnityEngine.Random.Range(0, SpawnPoints_Turrets.Count)].position;
                }

                SosigTemplate customTemplate = LoadedTemplateManager.LoadedSosigsDict[enemyTemplate];

                Sosig enemy = SpawnEnemy(customTemplate, character, AttackVectors[vectorIndex].SpawnPoints_Sosigs_Attack[vectorSpawnPoint], M.AI_Difficulty, curPhase.IFFUsed, true, targetVector, true);

                ActiveSosigs.Add(enemy);

                //At this point, the leader has been spawned, so always set enemy to be regulars
                enemyTemplate = ManagerSingleton<IM>.Instance.odicSosigObjsByID[(SosigEnemyID)LoadedTemplateManager.SosigIDDict[currPhase.EnemyType.GetRandom<string>()]];
                sosigsSpawned += 1;

                vectorIndex += 1;
                if (vectorIndex >= numAttackVectors)
                {
                    vectorIndex = 0;
                    vectorSpawnPoint += 1;
                }


            }
            isFirstWave = false;

        }



        [HarmonyPatch(typeof(TNH_HoldPoint), "SpawningRoutineUpdate")] // Specify target method with HarmonyPatch attribute
        [HarmonyPrefix]
        public static bool SpawningUpdateReplacement(
            ref float ___m_tickDownToNextGroupSpawn,
            List<Sosig> ___m_activeSosigs,
            TNH_HoldPoint.HoldState ___m_state,
            ref bool ___m_hasThrownNadesInWave,
            List<TNH_HoldPoint.AttackVector> ___AttackVectors,
            List<Transform> ___SpawnPoints_Turrets,
            TNH_Manager ___M,
            TNH_HoldChallenge.Phase ___m_curPhase,
            int ___m_phaseIndex,
            ref bool ___m_isFirstWave)
        {

            ___m_tickDownToNextGroupSpawn -= Time.deltaTime;


            if (___m_activeSosigs.Count < 1)
            {
                if (___m_state == TNH_HoldPoint.HoldState.Analyzing)
                {
                    ___m_tickDownToNextGroupSpawn -= Time.deltaTime;
                }
            }

            if (!___m_hasThrownNadesInWave && ___m_tickDownToNextGroupSpawn <= 5f && !___m_isFirstWave)
            {
                SpawnGrenades(___AttackVectors, ___M, ___m_phaseIndex);
                ___m_hasThrownNadesInWave = true;
            }

            //Handle spawning of a wave if it is time
            if (___m_tickDownToNextGroupSpawn <= 0 && ___m_activeSosigs.Count + ___m_curPhase.MaxEnemies <= ___m_curPhase.MaxEnemiesAlive)
            {
                ___AttackVectors.Shuffle();

                SpawnHoldEnemyGroup(___m_curPhase, ___m_phaseIndex, ___AttackVectors, ___SpawnPoints_Turrets, ___m_activeSosigs, ___M, ref ___m_isFirstWave);
                ___m_hasThrownNadesInWave = false;
                ___m_tickDownToNextGroupSpawn = ___m_curPhase.SpawnCadence;
            }


            return false;
        }




        /////////////////////////////
        //PATCHES FOR SPAWNING SOSIGS
        /////////////////////////////


        public static Sosig SpawnEnemy(SosigTemplate template, CustomCharacter character, Transform spawnLocation, TNHModifier_AIDifficulty difficulty, int IFF, bool isAssault, Vector3 pointOfInterest, bool allowAllWeapons)
        {
            if (character.ForceAllAgentWeapons) allowAllWeapons = true;

            TNHTweakerLogger.Log("TNHTWEAKER -- Spawning sosig: " + template.SosigEnemyID, TNHTweakerLogger.LogType.TNH);

            //Create the sosig object
            GameObject sosigPrefab = Instantiate(IM.OD[template.SosigPrefabs.GetRandom<string>()].GetGameObject(), spawnLocation.position, spawnLocation.rotation);
            Sosig sosigComponent = sosigPrefab.GetComponentInChildren<Sosig>();

            //Fill out the sosigs config based on the difficulty
            SosigConfig config;

            if (difficulty == TNHModifier_AIDifficulty.Arcade) config = template.ConfigsEasy.GetRandom<SosigConfig>();
            else config = template.Configs.GetRandom<SosigConfig>();
            sosigComponent.Configure(config.GetConfigTemplate());
            sosigComponent.E.IFFCode = IFF;

            //Setup the sosigs inventory
            sosigComponent.Inventory.Init();
            sosigComponent.Inventory.FillAllAmmo();
            sosigComponent.InitHands();

            //Equip the sosigs weapons
            if(template.WeaponOptions.Count > 0)
            {
                GameObject weaponPrefab = IM.OD[template.WeaponOptions.GetRandom<string>()].GetGameObject();
                EquipSosigWeapon(sosigComponent, weaponPrefab, difficulty);
            }

            if (template.WeaponOptionsSecondary.Count > 0 && allowAllWeapons && template.SecondaryChance >= UnityEngine.Random.value)
            {
                GameObject weaponPrefab = IM.OD[template.WeaponOptionsSecondary.GetRandom<string>()].GetGameObject();
                EquipSosigWeapon(sosigComponent, weaponPrefab, difficulty);
            }

            if (template.WeaponOptionsTertiary.Count > 0 && allowAllWeapons && template.TertiaryChance >= UnityEngine.Random.value)
            {
                GameObject weaponPrefab = IM.OD[template.WeaponOptionsTertiary.GetRandom<string>()].GetGameObject();
                EquipSosigWeapon(sosigComponent, weaponPrefab, difficulty);
            }

            //Equip clothing to the sosig
            OutfitConfig outfitConfig = template.OutfitConfigs.GetRandom<OutfitConfig>();
            if(outfitConfig.Chance_Headwear >= UnityEngine.Random.value)
            {
                EquipSosigClothing(outfitConfig.Headwear, sosigComponent.Links[0], outfitConfig.ForceWearAllHead);
            }

            if (outfitConfig.Chance_Facewear >= UnityEngine.Random.value)
            {
                EquipSosigClothing(outfitConfig.Facewear, sosigComponent.Links[0], outfitConfig.ForceWearAllFace);
            }

            if (outfitConfig.Chance_Eyewear >= UnityEngine.Random.value)
            {
                EquipSosigClothing(outfitConfig.Eyewear, sosigComponent.Links[0], outfitConfig.ForceWearAllEye);
            }

            if (outfitConfig.Chance_Torsowear >= UnityEngine.Random.value)
            {
                EquipSosigClothing(outfitConfig.Torsowear, sosigComponent.Links[1], outfitConfig.ForceWearAllTorso);
            }

            if (outfitConfig.Chance_Pantswear >= UnityEngine.Random.value)
            {
                EquipSosigClothing(outfitConfig.Pantswear, sosigComponent.Links[2], outfitConfig.ForceWearAllPants);
            }

            if (outfitConfig.Chance_Pantswear_Lower >= UnityEngine.Random.value)
            {
                EquipSosigClothing(outfitConfig.Pantswear_Lower, sosigComponent.Links[3], outfitConfig.ForceWearAllPantsLower);
            }

            if (outfitConfig.Chance_Backpacks >= UnityEngine.Random.value)
            {
                EquipSosigClothing(outfitConfig.Backpacks, sosigComponent.Links[1], outfitConfig.ForceWearAllBackpacks);
            }

            //Setup link spawns
            if (config.GetConfigTemplate().UsesLinkSpawns)
            {
                for(int i = 0; i < sosigComponent.Links.Count; i++)
                {
                    if(config.GetConfigTemplate().LinkSpawnChance[i] >= UnityEngine.Random.value)
                    {
                        if(config.GetConfigTemplate().LinkSpawns.Count > i && config.GetConfigTemplate().LinkSpawns[i] != null && config.GetConfigTemplate().LinkSpawns[i].Category != FVRObject.ObjectCategory.Loot)
                        {
                            sosigComponent.Links[i].RegisterSpawnOnDestroy(config.GetConfigTemplate().LinkSpawns[i]);
                        }
                    }
                }
            }

            //Setup the sosigs orders
            if (isAssault)
            {
                sosigComponent.CurrentOrder = Sosig.SosigOrder.Assault;
                sosigComponent.FallbackOrder = Sosig.SosigOrder.Assault;
                sosigComponent.CommandAssaultPoint(pointOfInterest);
            }
            else
            {
                sosigComponent.CurrentOrder = Sosig.SosigOrder.Wander;
                sosigComponent.FallbackOrder = Sosig.SosigOrder.Wander;
                sosigComponent.CommandGuardPoint(pointOfInterest, true);
                sosigComponent.SetDominantGuardDirection(UnityEngine.Random.onUnitSphere);
            }
            sosigComponent.SetGuardInvestigateDistanceThreshold(25f);

            //Handle sosig dropping custom loot
            if (UnityEngine.Random.value < template.DroppedLootChance && template.DroppedObjectPool != null)
            {
                string spawnedObject = template.DroppedObjectPool.GetObjects().GetRandom();

                if (LoadedTemplateManager.LoadedVaultFiles.ContainsKey(spawnedObject))
                {
                    TNHTweakerLogger.LogWarning("TNHTweaker -- Tried to add vaulted gun to sosigs dropped items, but spawning of vaulted items not supported yet! Nothing will be dropped!");
                }

                else
                {
                    sosigComponent.Links[2].RegisterSpawnOnDestroy(IM.OD[spawnedObject]);
                }
                
            }

            return sosigComponent;
        }


        [HarmonyPatch(typeof(FVRPlayerBody), "SetOutfit")] // Specify target method with HarmonyPatch attribute
        [HarmonyPrefix]
        public static bool SetOutfitReplacement(SosigEnemyTemplate tem, PlayerSosigBody ___m_sosigPlayerBody)
        {
            if (___m_sosigPlayerBody == null) return false;

            GM.Options.ControlOptions.MBClothing = tem.SosigEnemyID;
            if(tem.SosigEnemyID != SosigEnemyID.None)
            {
                if(tem.OutfitConfig.Count > 0 && LoadedTemplateManager.LoadedSosigsDict.ContainsKey(tem))
                {
                    OutfitConfig outfitConfig = LoadedTemplateManager.LoadedSosigsDict[tem].OutfitConfigs.GetRandom();

                    List<GameObject> clothing = Traverse.Create(___m_sosigPlayerBody).Field("m_curClothes").GetValue<List<GameObject>>();
                    foreach (GameObject item in clothing)
                    {
                        Destroy(item);
                    }
                    clothing.Clear();

                    if (outfitConfig.Chance_Headwear >= UnityEngine.Random.value)
                    {
                        EquipSosigClothing(outfitConfig.Headwear, clothing, ___m_sosigPlayerBody.Sosig_Head, outfitConfig.ForceWearAllHead);
                    }

                    if (outfitConfig.Chance_Facewear >= UnityEngine.Random.value)
                    {
                        EquipSosigClothing(outfitConfig.Facewear, clothing, ___m_sosigPlayerBody.Sosig_Head, outfitConfig.ForceWearAllFace);
                    }

                    if (outfitConfig.Chance_Eyewear >= UnityEngine.Random.value)
                    {
                        EquipSosigClothing(outfitConfig.Eyewear, clothing, ___m_sosigPlayerBody.Sosig_Head, outfitConfig.ForceWearAllEye);
                    }

                    if (outfitConfig.Chance_Torsowear >= UnityEngine.Random.value)
                    {
                        EquipSosigClothing(outfitConfig.Torsowear, clothing, ___m_sosigPlayerBody.Sosig_Torso, outfitConfig.ForceWearAllTorso);
                    }

                    if (outfitConfig.Chance_Pantswear >= UnityEngine.Random.value)
                    {
                        EquipSosigClothing(outfitConfig.Pantswear, clothing, ___m_sosigPlayerBody.Sosig_Abdomen, outfitConfig.ForceWearAllPants);
                    }

                    if (outfitConfig.Chance_Pantswear_Lower >= UnityEngine.Random.value)
                    {
                        EquipSosigClothing(outfitConfig.Pantswear_Lower, clothing, ___m_sosigPlayerBody.Sosig_Legs, outfitConfig.ForceWearAllPantsLower);
                    }

                    if (outfitConfig.Chance_Backpacks >= UnityEngine.Random.value)
                    {
                        EquipSosigClothing(outfitConfig.Backpacks, clothing, ___m_sosigPlayerBody.Sosig_Torso, outfitConfig.ForceWearAllBackpacks);
                    }

                }
            }

            return false;
        }


        public static void EquipSosigWeapon(Sosig sosig, GameObject weaponPrefab, TNHModifier_AIDifficulty difficulty)
        {
            SosigWeapon weapon = Instantiate(weaponPrefab, sosig.transform.position + Vector3.up * 0.1f, sosig.transform.rotation).GetComponent<SosigWeapon>();
            weapon.SetAutoDestroy(true);
            weapon.O.SpawnLockable = false;

            TNHTweakerLogger.Log("TNHTWEAKER -- Equipping sosig weapon: " + weapon.gameObject.name, TNHTweakerLogger.LogType.TNH);

            //Equip the sosig weapon to the sosig
            sosig.ForceEquip(weapon);
            weapon.SetAmmoClamping(true);
            if (difficulty == TNHModifier_AIDifficulty.Arcade) weapon.FlightVelocityMultiplier = 0.3f;
        }

        public static void EquipSosigClothing(List<string> options, SosigLink link, bool wearAll)
        {
            if (wearAll)
            {
                foreach(string clothing in options)
                {
                    GameObject clothingObject = Instantiate(IM.OD[clothing].GetGameObject(), link.transform.position, link.transform.rotation);
                    clothingObject.transform.SetParent(link.transform);
                    clothingObject.GetComponent<SosigWearable>().RegisterWearable(link);
                }
            }

            else
            {
                GameObject clothingObject = Instantiate(IM.OD[options.GetRandom<string>()].GetGameObject(), link.transform.position, link.transform.rotation);
                clothingObject.transform.SetParent(link.transform);
                clothingObject.GetComponent<SosigWearable>().RegisterWearable(link);
            }
        }


        public static void EquipSosigClothing(List<string> options, List<GameObject> playerClothing, Transform link,  bool wearAll)
        {
            if (wearAll)
            {
                foreach (string clothing in options)
                {
                    GameObject clothingObject = Instantiate(IM.OD[clothing].GetGameObject(), link.position, link.rotation);

                    Component[] children = clothingObject.GetComponentsInChildren<Component>(true);
                    foreach(Component child in children)
                    {
                        child.gameObject.layer = LayerMask.NameToLayer("ExternalCamOnly");

                        if(!(child is Transform) && !(child is MeshFilter) && !(child is MeshRenderer))
                        {
                            Destroy(child);
                        }
                    }

                    playerClothing.Add(clothingObject);
                    clothingObject.transform.SetParent(link);
                }
            }

            else
            {
                GameObject clothingObject = Instantiate(IM.OD[options.GetRandom<string>()].GetGameObject(), link.position, link.rotation);

                Component[] children = clothingObject.GetComponentsInChildren<Component>(true);
                foreach (Component child in children)
                {
                    child.gameObject.layer = LayerMask.NameToLayer("ExternalCamOnly");

                    if (!(child is Transform) && !(child is MeshFilter) && !(child is MeshRenderer))
                    {
                        Destroy(child);
                    }
                }

                playerClothing.Add(clothingObject);
                clothingObject.transform.SetParent(link);
            }
        }




        //////////////////////////////////////////////
        //PATCHES FOR CONSTRUCTOR AND SECONDARY PANELS
        //////////////////////////////////////////////


        [HarmonyPatch(typeof(TNH_ObjectConstructor), "ButtonClicked")] // Specify target method with HarmonyPatch attribute
        [HarmonyPrefix]
        public static bool ButtonClickedReplacement(int i,
            TNH_ObjectConstructor __instance,
            EquipmentPoolDef ___m_pool,
            int ___m_curLevel,
            ref int ___m_selectedEntry,
            ref int ___m_numTokensSelected,
            bool ___allowEntry,
            List<EquipmentPoolDef.PoolEntry> ___m_poolEntries,
            List<int> ___m_poolAddedCost,
            GameObject ___m_spawnedCase)
        {
            Traverse constructorTraverse = Traverse.Create(__instance);

            constructorTraverse.Method("UpdateRerollButtonState", false).GetValue();

            if (!___allowEntry)
            {
                return false;
            }
            
            if(__instance.State == TNH_ObjectConstructor.ConstructorState.EntryList)
            {

                int cost = ___m_poolEntries[i].GetCost(__instance.M.EquipmentMode) + ___m_poolAddedCost[i];
                if(__instance.M.GetNumTokens() >= cost)
                {
                    constructorTraverse.Method("SetState", TNH_ObjectConstructor.ConstructorState.Confirm, i).GetValue();
                    SM.PlayCoreSound(FVRPooledAudioType.UIChirp, __instance.AudEvent_Select, __instance.transform.position);
                }
                else
                {
                    SM.PlayCoreSound(FVRPooledAudioType.UIChirp, __instance.AudEvent_Fail, __instance.transform.position);
                }
            }

            else if(__instance.State == TNH_ObjectConstructor.ConstructorState.Confirm)
            {

                if (i == 0)
                {
                    constructorTraverse.Method("SetState", TNH_ObjectConstructor.ConstructorState.EntryList, 0).GetValue();
                    ___m_selectedEntry = -1;
                    SM.PlayCoreSound(FVRPooledAudioType.UIChirp, __instance.AudEvent_Back, __instance.transform.position);
                }
                else if(i == 2)
                {
                    int cost = ___m_poolEntries[___m_selectedEntry].GetCost(__instance.M.EquipmentMode) + ___m_poolAddedCost[___m_selectedEntry];
                    if (__instance.M.GetNumTokens() >= cost)
                    {

                        if ((!___m_poolEntries[___m_selectedEntry].TableDef.SpawnsInSmallCase && !___m_poolEntries[___m_selectedEntry].TableDef.SpawnsInSmallCase) || ___m_spawnedCase == null)
                        {

                            AnvilManager.Run(SpawnObjectAtConstructor(___m_poolEntries[___m_selectedEntry], __instance, constructorTraverse));
                            ___m_numTokensSelected = 0;
                            __instance.M.SubtractTokens(cost);
                            SM.PlayCoreSound(FVRPooledAudioType.UIChirp, __instance.AudEvent_Spawn, __instance.transform.position);

                            if (__instance.M.C.UsesPurchasePriceIncrement)
                            {
                                ___m_poolAddedCost[___m_selectedEntry] += 1;
                            }

                            constructorTraverse.Method("SetState", TNH_ObjectConstructor.ConstructorState.EntryList, 0).GetValue();
                            ___m_selectedEntry = -1;
                        }

                        else
                        {
                            SM.PlayCoreSound(FVRPooledAudioType.UIChirp, __instance.AudEvent_Fail, __instance.transform.position);
                        }
                    }
                    else
                    {
                        SM.PlayCoreSound(FVRPooledAudioType.UIChirp, __instance.AudEvent_Fail, __instance.transform.position);
                    }
                }
            }

            return false;
        }


        private static IEnumerator SpawnObjectAtConstructor(EquipmentPoolDef.PoolEntry entry, TNH_ObjectConstructor constructor, Traverse constructorTraverse)
        {
            TNHTweakerLogger.Log("TNHTWEAKER -- Spawning item at constructor", TNHTweakerLogger.LogType.TNH);

            constructorTraverse.Field("allowEntry").SetValue(false);
            EquipmentPool pool = LoadedTemplateManager.EquipmentPoolDictionary[entry];
            CustomCharacter character = LoadedTemplateManager.LoadedCharactersDict[constructor.M.C];
            List<GameObject> trackedObjects = (List<GameObject>)(constructorTraverse.Field("m_trackedObjects").GetValue());

            if(pool.Tables[0].SpawnsInLargeCase || pool.Tables[0].SpawnsInSmallCase)
            {
                TNHTweakerLogger.Log("TNHTWEAKER -- Item will spawn in a container", TNHTweakerLogger.LogType.TNH);

                GameObject caseFab = constructor.M.Prefab_WeaponCaseLarge;
                if (pool.Tables[0].SpawnsInSmallCase) caseFab = constructor.M.Prefab_WeaponCaseSmall;

                FVRObject item = IM.OD[pool.Tables[0].GetObjects().GetRandom()];
                GameObject itemCase = constructor.M.SpawnWeaponCase(caseFab, constructor.SpawnPoint_Case.position, constructor.SpawnPoint_Case.forward, item, pool.Tables[0].NumMagsSpawned, pool.Tables[0].NumRoundsSpawned, pool.Tables[0].MinAmmoCapacity, pool.Tables[0].MaxAmmoCapacity);

                constructorTraverse.Field("m_spawnedCase").SetValue(itemCase);
                itemCase.GetComponent<TNH_WeaponCrate>().M = constructor.M;
            }

            else
            {
                TNHTweakerLogger.Log("TNHTWEAKER -- Item will spawn without a container", TNHTweakerLogger.LogType.TNH);

                int mainSpawnCount = 0;
                int requiredSpawnCount = 0;
                int ammoSpawnCount = 0;
                int objectSpawnCount = 0;


                TNHTweakerLogger.Log("TNHTWEAKER -- Pool has " + pool.Tables.Count + " tables to spawn from" ,TNHTweakerLogger.LogType.TNH);
                for (int tableIndex = 0; tableIndex < pool.Tables.Count; tableIndex++)
                {
                    ObjectPool table = pool.Tables[tableIndex];

                    TNHTweakerLogger.Log("TNHTWEAKER -- Table will spawn " + table.ItemsToSpawn + " items from it", TNHTweakerLogger.LogType.TNH);
                    for (int itemIndex = 0; itemIndex < table.ItemsToSpawn; itemIndex++)
                    {
                        FVRObject mainObject;
                        SavedGunSerializable vaultFile = null;

                        Transform primarySpawn = constructor.SpawnPoint_Object;
                        Transform requiredSpawn = constructor.SpawnPoint_Object;
                        Transform ammoSpawn = constructor.SpawnPoint_Mag;

                        if (table.IsCompatibleMagazine)
                        {
                            TNHTweakerLogger.Log("TNHTWEAKER -- Item will be a compatible magazine", TNHTweakerLogger.LogType.TNH);
                            mainObject = FirearmUtils.GetMagazineForEquipped(table.MinAmmoCapacity, table.MaxAmmoCapacity);
                            if (mainObject == null)
                            {
                                TNHTweakerLogger.LogWarning("TNHTWEAKER -- Failed to spawn a compatible magazine!");
                                break;
                            }
                        }

                        else
                        {
                            string item = table.GetObjects().GetRandom();
                            TNHTweakerLogger.Log("TNHTWEAKER -- Item selected: " + item, TNHTweakerLogger.LogType.TNH);

                            if (LoadedTemplateManager.LoadedVaultFiles.ContainsKey(item))
                            {
                                TNHTweakerLogger.Log("TNHTWEAKER -- Item is a vaulted gun", TNHTweakerLogger.LogType.TNH);
                                vaultFile = LoadedTemplateManager.LoadedVaultFiles[item];
                                mainObject = vaultFile.GetGunObject();
                            }

                            else
                            {
                                TNHTweakerLogger.Log("TNHTWEAKER -- Item is a normal object", TNHTweakerLogger.LogType.TNH);
                                mainObject = IM.OD[item];
                            }
                        }

                        //Assign spawn points based on the type of item we are spawning
                        if (mainObject.Category == FVRObject.ObjectCategory.Firearm)
                        {
                            primarySpawn = constructor.SpawnPoints_GunsSize[mainObject.TagFirearmSize - FVRObject.OTagFirearmSize.Pocket];
                            requiredSpawn = constructor.SpawnPoint_Grenade;
                            mainSpawnCount += 1;
                        }
                        else if (mainObject.Category == FVRObject.ObjectCategory.Explosive || mainObject.Category == FVRObject.ObjectCategory.Thrown)
                        {
                            primarySpawn = constructor.SpawnPoint_Grenade;
                        }
                        else if (mainObject.Category == FVRObject.ObjectCategory.MeleeWeapon)
                        {
                            primarySpawn = constructor.SpawnPoint_Melee;
                        }


                        //If this is a vault file, we have to spawn it through a routine. Otherwise we just instantiate it
                        if (vaultFile != null)
                        {
                            AnvilManager.Run(TNHTweakerUtils.SpawnFirearm(vaultFile, primarySpawn, trackedObjects));
                            TNHTweakerLogger.Log("TNHTWEAKER -- Vaulted gun spawned", TNHTweakerLogger.LogType.TNH);
                        }
                        else
                        {
                            yield return mainObject.GetGameObjectAsync();
                            GameObject spawnedObject = Instantiate(mainObject.GetGameObject(), primarySpawn.position + Vector3.up * 0.2f * mainSpawnCount, primarySpawn.rotation);
                            trackedObjects.Add(spawnedObject);
                            TNHTweakerLogger.Log("TNHTWEAKER -- Normal item spawned", TNHTweakerLogger.LogType.TNH);
                        }

                        
                        //Spawn any required objects
                        for (int j = 0; j < mainObject.RequiredSecondaryPieces.Count; j++)
                        {
                            yield return mainObject.RequiredSecondaryPieces[j].GetGameObjectAsync();
                            GameObject requiredItem = Instantiate(mainObject.RequiredSecondaryPieces[j].GetGameObject(), requiredSpawn.position + -requiredSpawn.right * 0.2f * requiredSpawnCount + Vector3.up * 0.2f * j, requiredSpawn.rotation);
                            trackedObjects.Add(requiredItem);
                            requiredSpawnCount += 1;
                            TNHTweakerLogger.Log("TNHTWEAKER -- Required item spawned", TNHTweakerLogger.LogType.TNH);
                        }


                        //If this object has compatible ammo object, then we should spawn those
                        FVRObject ammoObject = mainObject.GetRandomAmmoObject(mainObject, character.ValidAmmoEras, table.MinAmmoCapacity, table.MaxAmmoCapacity, character.ValidAmmoSets);
                        if (ammoObject != null)
                        {
                            TNHTweakerLogger.Log("TNHTWEAKER -- Item has compatible ammo object: " + ammoObject.ItemID, TNHTweakerLogger.LogType.TNH);

                            int spawnCount = table.NumMagsSpawned;

                            if (ammoObject.Category == FVRObject.ObjectCategory.Cartridge)
                            {
                                ammoSpawn = constructor.SpawnPoint_Ammo;
                                spawnCount = table.NumRoundsSpawned;
                            }

                            yield return ammoObject.GetGameObjectAsync();

                            for (int j = 0; j < spawnCount; j++)
                            {
                                GameObject spawnedAmmo = Instantiate(ammoObject.GetGameObject(), ammoSpawn.position + -ammoSpawn.right * 0.15f * ammoSpawnCount + ammoSpawn.up * 0.15f * j, ammoSpawn.rotation);
                                trackedObjects.Add(spawnedAmmo);
                            }

                            ammoSpawnCount += 1;
                        }


                        //If this object equires picatinny sights, we should try to spawn one
                        if (mainObject.RequiresPicatinnySight && character.GetRequiredSightsTable() != null)
                        {
                            FVRObject sight = character.GetRequiredSightsTable().GetRandomObject();
                            yield return sight.GetGameObjectAsync();
                            GameObject spawnedSight = Instantiate(sight.GetGameObject(), constructor.SpawnPoint_Object.position + -constructor.SpawnPoint_Object.right * 0.15f * objectSpawnCount, constructor.SpawnPoint_Object.rotation);
                            trackedObjects.Add(spawnedSight);

                            TNHTweakerLogger.Log("TNHTWEAKER -- Required sight spawned", TNHTweakerLogger.LogType.TNH);

                            for (int j = 0; j < sight.RequiredSecondaryPieces.Count; j++)
                            {
                                yield return sight.RequiredSecondaryPieces[j].GetGameObjectAsync();
                                GameObject spawnedRequired = Instantiate(sight.RequiredSecondaryPieces[j].GetGameObject(), constructor.SpawnPoint_Object.position + -constructor.SpawnPoint_Object.right * 0.15f * objectSpawnCount + Vector3.up * 0.15f * j, constructor.SpawnPoint_Object.rotation);
                                trackedObjects.Add(spawnedRequired);
                                TNHTweakerLogger.Log("TNHTWEAKER -- Required item for sight spawned", TNHTweakerLogger.LogType.TNH);
                            }

                            objectSpawnCount += 1;
                        }

                        //If this object has bespoke attachments we'll try to spawn one
                        else if (mainObject.BespokeAttachments.Count > 0 && UnityEngine.Random.value < table.BespokeAttachmentChance)
                        {
                            FVRObject bespoke = mainObject.BespokeAttachments.GetRandom();
                            yield return bespoke.GetGameObjectAsync();
                            GameObject bespokeObject = Instantiate(bespoke.GetGameObject(), constructor.SpawnPoint_Object.position + -constructor.SpawnPoint_Object.right * 0.15f * objectSpawnCount, constructor.SpawnPoint_Object.rotation);
                            trackedObjects.Add(bespokeObject);
                            objectSpawnCount += 1;

                            TNHTweakerLogger.Log("TNHTWEAKER -- Bespoke item spawned", TNHTweakerLogger.LogType.TNH);
                        }
                    }
                }
            }

            constructorTraverse.Field("allowEntry").SetValue(true);
            yield break;
        }



        //////////////////////////
        //MISC PATCHES AND METHODS
        //////////////////////////


        [HarmonyPatch(typeof(TNH_Manager), "SetPhase_Hold")] // Specify target method with HarmonyPatch attribute
        [HarmonyPostfix]
        public static void AfterSetHold()
        {
            ClearAllPanels();
        }

        [HarmonyPatch(typeof(TNH_Manager), "SetPhase_Dead")] // Specify target method with HarmonyPatch attribute
        [HarmonyPostfix]
        public static void AfterSetDead()
        {
            ClearAllPanels();
        }

        [HarmonyPatch(typeof(TNH_Manager), "SetPhase_Completed")] // Specify target method with HarmonyPatch attribute
        [HarmonyPostfix]
        public static void AfterSetComplete()
        {
            ClearAllPanels();
        }

        public static void ClearAllPanels()
        {
            //Debug.Log("Destroying constructors");
            while (SpawnedConstructors.Count > 0)
            {
                try
                {
                    TNH_ObjectConstructor constructor = SpawnedConstructors[0].GetComponent<TNH_ObjectConstructor>();

                    if (constructor != null)
                    {
                        constructor.ClearCase();
                    }

                    Destroy(SpawnedConstructors[0]);
                }
                catch
                {
                    TNHTweakerLogger.LogWarning("TNHTWEAKER -- Failed to destroy constructor! It's likely that the constructor is already destroyed, so everything is probably just fine :)");
                }

                SpawnedConstructors.RemoveAt(0);
            }

            //Debug.Log("Destroying panels");
            while (SpawnedPanels.Count > 0)
            {
                Destroy(SpawnedPanels[0]);
                SpawnedPanels.RemoveAt(0);
            }
        }

        [HarmonyPatch(typeof(Sosig), "BuffHealing_Invis")] // Specify target method with HarmonyPatch attribute
        [HarmonyPrefix]
        public static bool OverrideCloaking()
        {
            return !preventOutfitFunctionality;
        }


    }
}