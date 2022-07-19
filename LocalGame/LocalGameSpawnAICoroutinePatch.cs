﻿using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using SIT.Coop.Core.Player;
using SIT.Coop.Core.Web;
using SIT.Tarkov.Core;
using SIT.Tarkov.Core.AI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace SIT.Coop.Core.LocalGame
{
    internal class LocalGameSpawnAICoroutinePatch : ModulePatch
    {
        /*
        protected virtual IEnumerator vmethod_3(float startDelay, GStruct252 controllerSettings, GInterface250 spawnSystem, Callback runCallback)
        */
        public static object BossSpawner;
        public static object BossSpawnerWaves;
        private static ConfigFile _config;
        private static MethodInfo MethodInfoBotCreation;
        private static int maxCountOfBots = 20;


        public LocalGameSpawnAICoroutinePatch(ConfigFile config)
        {
            _config = config;
        }

        protected override MethodBase GetTargetMethod()
        {
            BossSpawner = PatchConstants.GetFieldFromType(LocalGamePatches.LocalGameInstance.GetType().BaseType
                , BotSystemHelpers.BossSpawnRunnerType.Name.ToLower() + "_0").GetValue(LocalGamePatches.LocalGameInstance);
            BossSpawnerWaves = PatchConstants.GetFieldOrPropertyFromInstance<object>(BossSpawner, "BossSpawnWaves", false);

            MethodInfoBotCreation = PatchConstants.GetMethodForType(LocalGamePatches.LocalGameInstance.GetType().BaseType, "method_8");

            return PatchConstants.GetAllMethodsForType(LocalGamePatches.LocalGameInstance.GetType().BaseType)
                .Single(
                m =>
                m.IsVirtual
                && m.GetParameters().Length >= 4
                && m.GetParameters()[0].ParameterType == typeof(float)
                && m.GetParameters()[0].Name == "startDelay"
                && m.GetParameters()[1].Name == "controllerSettings"
                );
        }

        //[PatchPrefix]
        //public static bool PatchPrefix(
        //float startDelay, object controllerSettings, object spawnSystem, object runCallback,
        //object ___wavesSpawnScenario_0, ref IEnumerator __result
        //)
        //{
        //    Logger.LogInfo("LocalGameSpawnAICoroutinePatch.PatchPrefix");

        //    return true;
        //}

        [PatchPostfix]
        //public static IEnumerator PatchPostfix(
        //float startDelay, object controllerSettings, object spawnSystem, object runCallback,
        //GClass1222 ___gclass1222_0
        //)
        public static IEnumerator PatchPostfix(
            IEnumerator __result,
            //GClass1222 ___gclass1222_0,
            //GClass543 ___gclass543_0,
            object spawnSystem,
            Callback runCallback,
            WavesSpawnScenario ___wavesSpawnScenario_0,
            NonWavesSpawnScenario ___nonWavesSpawnScenario_0
        )
        {
            BotSystemHelpers.BotControllerInstance = PatchConstants.GetFieldOrPropertyFromInstance<object>(
                   LocalGamePatches.LocalGameInstance
                   , BotSystemHelpers.BotControllerType.Name + "_0"
                   //, "gclass1222_0"
                   , false);
            //Logger.LogInfo($"bossSpawner:{BossSpawner}");

            // Run normally.
            if (!Matchmaker.MatchmakerAcceptPatches.IsClient)
            {
                //CoopGameComponent.Players.Clear();
                var nonSpawnWaveShit = PatchConstants.GetFieldOrPropertyFromInstance<object>(
                    LocalGamePatches.LocalGameInstance
                    , BotSystemHelpers.RoleLimitDifficultyType.Name + "_0"
                    , false);
                var openZones = PatchConstants.GetFieldOrPropertyFromInstance<object>(LocalGamePatches.LocalGameInstance, BotSystemHelpers.LocationBaseType.Name + "_0", false);
                var openZones2 = PatchConstants.GetFieldOrPropertyFromInstance<string>(openZones, "OpenZones", false);

                // Construct Profile Creator
                var profileCreator = Activator.CreateInstance(
                    BotSystemHelpers.ProfileCreatorType
                    , PatchConstants.BackEndSession
                    , ___wavesSpawnScenario_0.SpawnWaves
                    , BossSpawnerWaves
                    , nonSpawnWaveShit
                    , false
                    );

                // Construct Bot Creator
                var botCreator = Activator.CreateInstance(
                    BotSystemHelpers.BotCreatorType
                    , LocalGamePatches.LocalGameInstance
                    , profileCreator
                    ,
                    PatchConstants.GetMethodForType(typeof(LocalGameSpawnAICoroutinePatch), "BotCreationMethod").CreateDelegate(LocalGamePatches.LocalGameInstance)

                    );

                BotZone[] botZones = LocationScene.GetAllObjects<BotZone>().ToArray();
                bool enableWaveControl = true;


                // Need to get this.gclass1222_0. and assign it in BotSystemHelpers first !
                // TODO: Make this not directly go for gclass1222_0
                BotSystemHelpers.BotControllerInstance = PatchConstants.GetFieldOrPropertyFromInstance<object>(
                    LocalGamePatches.LocalGameInstance
                    , BotSystemHelpers.BotControllerType.Name + "_0"
                    //, "gclass1222_0"
                    , false);
                BotSystemHelpers.Init(LocalGamePatches.LocalGameInstance, botCreator, botZones, spawnSystem, ___wavesSpawnScenario_0.BotLocationModifier, true, false, enableWaveControl, false, false, Singleton<GameWorld>.Instance
                    , openZones2);


                // TODO: Max this use the EBotAmount from controllerSettings
                var AICountOverride = _config.Bind("Server", "Override Number of AI", false).Value;
                var NumberOfAI = _config.Bind("Server", "Number of AI", 15).Value;
                if (AICountOverride)
                {
                    maxCountOfBots = NumberOfAI;
                }
                var backendConfig = PatchConstants.GetFieldOrPropertyFromInstance<object>(PatchConstants.BackEndSession, "BackEndConfig", false);
                var botPresets = PatchConstants.GetFieldOrPropertyFromInstance<Array>(backendConfig, "BotPresets", false);
                var botWeaponScatterings = PatchConstants.GetFieldOrPropertyFromInstance<Array>(backendConfig, "BotWeaponScatterings", false);

                Logger.LogInfo($"Max Number of Bots:{maxCountOfBots}");
                BotSystemHelpers.SetSettings(maxCountOfBots, botPresets, botWeaponScatterings);
                var AIIgnorePlayers = _config.Bind("Server", "AI Ignore Players", false).Value;
                if(!AIIgnorePlayers)
                {
                    var gparam = PatchConstants.GetFieldOrPropertyFromInstance<object>(LocalGamePatches.LocalGameInstance, "gparam_0", false);
                    var player = PatchConstants.GetFieldOrPropertyFromInstance<EFT.Player>(gparam, "Player", false);
                    BotSystemHelpers.AddActivePlayer(player);
                }
                yield return new WaitForSeconds(1);

                if (___wavesSpawnScenario_0.SpawnWaves != null && ___wavesSpawnScenario_0.SpawnWaves.Length != 0)
                {
                    ___wavesSpawnScenario_0.Run();
                }
                else
                {
                    ___nonWavesSpawnScenario_0.Run();
                }
                yield return new WaitForSeconds(1);

                PatchConstants.GetMethodForType(BossSpawner.GetType(), "Run").Invoke(BossSpawner, new object[] { EBotsSpawnMode.Anyway });
                yield return new WaitForSeconds(1);

                using (PatchConstants.StartWithToken("SessionRun"))
                {
                    // TODO: This needs removing!
                    PatchConstants.GetMethodForType(LocalGamePatches.LocalGameInstance.GetType(), "vmethod_4").Invoke(LocalGamePatches.LocalGameInstance, new object[0]);
                }
                yield return new WaitForSeconds(1);

                runCallback.Succeed();
            }
            else 
            {
                BotSystemHelpers.SetSettingsNoBots();
                try
                {
                    BotSystemHelpers.Stop();
                }
                catch
                {

                }
                yield return new WaitForSeconds(1);
                using (PatchConstants.StartWithToken("SessionRun"))
                {
                    // TODO: This needs removing!
                    PatchConstants.GetMethodForType(LocalGamePatches.LocalGameInstance.GetType(), "vmethod_4").Invoke(LocalGamePatches.LocalGameInstance, new object[0]);
                }
                runCallback.Succeed();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="profile"></param>
        /// <param name="position"></param>
        /// <returns></returns>
        public static async Task<LocalPlayer> BotCreationMethod(Profile profile, Vector3 position)
        {
            // If its Client. Out!
            if (Matchmaker.MatchmakerAcceptPatches.IsClient)
                return null;

            // If there is no profile. Out!
            if (profile == null)
                return null;

            if (CoopGameComponent.Players.Count >= maxCountOfBots) 
            {
                //Logger.LogInfo($"BotCreationMethod. [ERROR] CoopGameComponent.Players is full");
                return null;
            }

            if (CoopGameComponent.Players.ContainsKey(profile.AccountId))
            {
                //Logger.LogInfo($"BotCreationMethod. [ERROR] Bot already exists");
                return null;
            }

            // TODO: Rewrite the following method into into BotCreationMethod
            var player = await (Task<LocalPlayer>)PatchConstants.GetMethodForType(LocalGamePatches.LocalGameInstance.GetType().BaseType, "method_8")
            .Invoke(LocalGamePatches.LocalGameInstance, new object[] { profile, position });

            var prc = player.GetOrAddComponent<PlayerReplicatedComponent>();
            prc.player = player;
            CoopGameComponent.Players.TryAdd(profile.AccountId, player);
            Dictionary<string, object> dictionary2 = new Dictionary<string, object>
                    {
                        {
                            "SERVER",
                            "SERVER"
                        },
                        {
                            "accountId",
                            player.Profile.AccountId
                        },
                        {
                            "profileId",
                            player.ProfileId
                        },
                        {
                            "groupId",
                            Matchmaker.MatchmakerAcceptPatches.GetGroupId()
                        },
                        {
                            "nP",
                            position
                        },
                        {
                            "sP",
                            position
                        },
                        { "m", "PlayerSpawn" },
                        {
                            "p.info",
                            player.Profile.Info.SITToJson()
                        },
                        {
                            "p.cust",
                             player.Profile.Customization.SITToJson()
                        },
                        {
                            "p.equip",
                            player.Profile.Inventory.Equipment.CloneItem().ToJson()
                        }
                    };
            //new Request().PostJson("/client/match/group/server/players/spawn", dictionary2.ToJson());
            ServerCommunication.PostLocalPlayerData(player, dictionary2);
            //Logger.LogInfo($"BotCreationMethod. [SUCCESS] Adding AI {profile.AccountId} to CoopGameComponent.Players list");
            return player;
        }
    }
}
