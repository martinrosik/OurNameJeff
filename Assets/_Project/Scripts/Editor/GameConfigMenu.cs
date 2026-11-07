using System.Collections.Generic;
using ResortEmpire.Core;
using UnityEditor;
using UnityEngine;

namespace ResortEmpire.Editor
{
    /// <summary>Config and balance tools under "Resort Empire" in the Editor menu bar.</summary>
    internal static class GameConfigMenu
    {
        [MenuItem("Resort Empire/Config/Select Prototype Config", priority = 100)]
        private static void SelectConfig()
        {
            GameConfig config = LoadPrototypeConfig();
            if (config != null)
            {
                Selection.activeObject = config;
                EditorGUIUtility.PingObject(config);
            }
            else
            {
                Debug.LogWarning($"No config at {PrototypeConfigDefaults.AssetPath}. Use Resort Empire/Config/Create Prototype Config.");
            }
        }

        [MenuItem("Resort Empire/Config/Create Prototype Config", priority = 101)]
        private static void CreateConfig()
        {
            if (LoadPrototypeConfig() != null)
            {
                Debug.LogWarning($"{PrototypeConfigDefaults.AssetPath} already exists. Delete it first to reset it to the Build doc values.");
                SelectConfig();
                return;
            }

            GameConfig config = PrototypeConfigDefaults.Create();
            AssetDatabase.CreateAsset(config, PrototypeConfigDefaults.AssetPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"Created {PrototypeConfigDefaults.AssetPath} with the Build doc values.");
            SelectConfig();
        }

        [MenuItem("Resort Empire/Config/Validate Prototype Config", priority = 102)]
        private static void ValidateConfig()
        {
            GameConfig config = LoadPrototypeConfig();
            List<string> errors = GameConfigValidator.Validate(config);
            if (errors.Count == 0)
            {
                Debug.Log("Prototype config is valid (WR1, WR2 and data checks pass).");
            }
            else
            {
                Debug.LogError("Prototype config has problems:\n- " + string.Join("\n- ", errors), config);
            }
        }

        [MenuItem("Resort Empire/Balance/Wave Balance Report", priority = 200)]
        private static void RunBalanceReport()
        {
            GameConfig config = LoadPrototypeConfig();
            if (config == null)
            {
                Debug.LogError($"No config at {PrototypeConfigDefaults.AssetPath}.");
                return;
            }

            WaveBalanceReport report = WaveBalanceSimulator.BuildReport(config);
            string message = "Wave balance (passive player: one placement, no moves, upgrades or Energy)\n" + report.ToTable()
                             + $"\nPassive Coins waves 1–3: {report.PassiveCoins(1, 3)}, waves 4–6: {report.PassiveCoins(4, 6)}";
            if (report.HasViolations)
            {
                Debug.LogWarning(message, config);
            }
            else
            {
                Debug.Log(message, config);
            }
        }

        public static GameConfig LoadPrototypeConfig() =>
            AssetDatabase.LoadAssetAtPath<GameConfig>(PrototypeConfigDefaults.AssetPath);
    }
}
