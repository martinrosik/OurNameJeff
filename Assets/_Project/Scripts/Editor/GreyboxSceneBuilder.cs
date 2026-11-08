using System.Collections.Generic;
using ResortEmpire.Core;
using ResortEmpire.Game;
using ResortEmpire.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace ResortEmpire.Editor
{
    /// <summary>
    /// Lays out the greybox in Game.unity (OUR-31): Game (controller), Island (sand, grass, palms, rocks, bushes,
    /// dock, guest boat and the buildings from the config, with placeholder Kenney art and their Worker and
    /// capacity indicators) and UI. Re-running it replaces those three roots, so local tweaks to them are lost.
    /// Move things by hand afterwards if you like; the code doesn't depend on positions.
    /// </summary>
    public static class GreyboxSceneBuilder
    {
        private const string GameScenePath = "Assets/_Project/Scenes/Game.unity";
        private const string KenneyCity = "Assets/_Project/Art/Kenney/City/";
        private const string KenneyPirate = "Assets/_Project/Art/Kenney/Pirate/";

        // Island centre sits left of the screen centre: the side panel covers the right ~35 %.
        private static readonly Vector3 IslandCentre = new Vector3(-3.6f, -0.2f, 0f);

        // Building layout (local to the building): name, art, a patio plate with the seats underneath.
        // OUR-38 will add the queue below the plate (around y -1.1).
        private static readonly Vector2 PlateSize = new Vector2(2.3f, 0.5f);
        private const float PlateY = -0.5f;
        private const float ArtY = 0.42f;
        private const float NameY = 1.5f;
        private const float StatusY = 1.18f;

        private const int DecorOrder = 2;
        private const int PlateOrder = 5;
        private const int ArtOrder = 6;
        private const int BadgeOrder = 8;
        private const int LabelBackingOrder = 9;

        [MenuItem("Resort Empire/Greybox/Rebuild Greybox Scene", priority = 301)]
        public static void Build()
        {
            if (GreyboxArtGenerator.Square == null || GreyboxArtGenerator.Rounded == null || GreyboxArtGenerator.Circle == null)
            {
                GreyboxArtGenerator.Generate();
            }

            if (string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(PrototypeConfigDefaults.AssetPath, AssetPathToGUIDOptions.OnlyExistingAssets)))
            {
                Debug.LogError($"Missing {PrototypeConfigDefaults.AssetPath}. Use Resort Empire/Config/Create Prototype Config first.");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);

            // Load assets only after opening the scene: OpenScene unloads unused assets, which would leave
            // references loaded earlier pointing at destroyed objects (and saving them as missing).
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(PrototypeConfigDefaults.AssetPath);

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == "Game" || root.name == "Island" || root.name == "UI")
                {
                    Object.DestroyImmediate(root);
                }
            }

            var gameObject = new GameObject("Game");
            var controller = gameObject.AddComponent<GameController>();
            SetReference(controller, "config", config);

            BuildIsland(controller, config);

            // Nothing on the island is tappable yet (moving Workers by tapping a building is OUR-45).
            Camera camera = Object.FindAnyObjectByType<Camera>();
            if (camera != null && camera.TryGetComponent(out Physics2DRaycaster raycaster))
            {
                Object.DestroyImmediate(raycaster);
            }

            var uiObject = new GameObject("UI");
            var ui = uiObject.AddComponent<GreyboxUI>();
            SetReference(ui, "controller", controller);
            SetReference(ui, "roundedSprite", GreyboxArtGenerator.Rounded);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Greybox scene rebuilt.");
        }

        private static void BuildIsland(GameController controller, GameConfig config)
        {
            var root = new GameObject("Island");
            var island = root.AddComponent<IslandView>();
            SetReference(island, "controller", controller);

            Shape(root.transform, "Sand", GreyboxArtGenerator.Circle, GreyboxPalette.Sand, IslandCentre, new Vector2(10.6f, 8.2f), 0);
            Shape(root.transform, "Grass", GreyboxArtGenerator.Circle, GreyboxPalette.Grass, IslandCentre + new Vector3(-0.1f, 0.35f, 0f), new Vector2(8.6f, 6.1f), 1);

            var decor = new GameObject("Decor").transform;
            decor.SetParent(root.transform, false);
            Art(decor, "Palm", KenneyPirate + "palm-detailed-straight.png", IslandCentre + new Vector3(-4.45f, 0.1f, 0f), 0.95f, DecorOrder);
            Art(decor, "Palm", KenneyPirate + "palm-detailed-bend.png", IslandCentre + new Vector3(4.45f, -0.1f, 0f), 0.95f, DecorOrder);
            Art(decor, "Palm", KenneyPirate + "palm-bend.png", IslandCentre + new Vector3(3.75f, -2.55f, 0f), 0.85f, DecorOrder);
            Art(decor, "Palm", KenneyPirate + "palm-straight.png", IslandCentre + new Vector3(-3.75f, -2.45f, 0f), 0.95f, DecorOrder);
            Art(decor, "Rocks", KenneyPirate + "rocks-sand-a.png", IslandCentre + new Vector3(-1.1f, -3.45f, 0f), 0.75f, DecorOrder);
            Art(decor, "Rocks", KenneyPirate + "rocks-sand-a.png", IslandCentre + new Vector3(4.35f, -1.55f, 0f), 0.55f, DecorOrder);
            Art(decor, "Bush", KenneyPirate + "grass-plant.png", IslandCentre + new Vector3(-1.5f, 3.15f, 0f), 0.6f, DecorOrder);
            Art(decor, "Bush", KenneyPirate + "grass-plant.png", IslandCentre + new Vector3(1.5f, 3.2f, 0f), 0.55f, DecorOrder);
            Art(decor, "Dock", KenneyPirate + "structure-platform-dock.png", IslandCentre + new Vector3(0.25f, -3.55f, 0f), 1.15f, DecorOrder);

            foreach (BuildingDefinition definition in config.buildings)
            {
                BuildBuilding(root.transform, definition);
            }

            var boatObject = new GameObject("Boat");
            boatObject.transform.SetParent(root.transform, false);
            var boat = boatObject.AddComponent<BoatView>();
            GameObject ship = Art(boatObject.transform, "Ship", KenneyPirate + "ship-small.png", Vector3.zero, 1.3f, 4).gameObject;
            SetReference(boat, "visual", ship);
            SetVector3(boat, "dockPosition", IslandCentre + new Vector3(1.5f, -3.85f, 0f));
            SetVector3(boat, "seaPosition", IslandCentre + new Vector3(5.2f, -6.4f, 0f));
            SetReference(island, "boat", boat);
        }

        private static void BuildBuilding(Transform parent, BuildingDefinition definition)
        {
            var root = new GameObject($"Building_{definition.building}");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = IslandCentre + Offset(definition.building);

            var view = root.AddComponent<BuildingView>();

            List<SpriteRenderer> art = BuildArt(root.transform, definition.building);

            // OUR-50: the seats sit on this plate (one square per seat, created by BuildingView).
            SpriteRenderer plate = Sliced(root.transform, "Plate", GreyboxPalette.ForBuilding(definition.building), new Vector3(0f, PlateY, 0f), PlateSize, PlateOrder);
            var markers = new GameObject("Seats").transform;
            markers.SetParent(root.transform, false);

            // OUR-48: Worker badge ("W" / grey "-") and the "No Worker" label.
            SpriteRenderer badge = Shape(root.transform, "WorkerBadge", GreyboxArtGenerator.Circle, GreyboxPalette.WorkerOn, new Vector3(1.0f, 0.95f, 0f), new Vector2(0.46f, 0.46f), BadgeOrder);
            TMP_Text workerLabel = Label(badge.transform, "WorkerLabel", "W", 2.8f, Color.white, Vector3.zero, new Vector2(1f, 1f), FontStyles.Bold);
            workerLabel.transform.parent.localScale = new Vector3(1f / 0.46f, 1f / 0.46f, 1f);

            Sliced(root.transform, "NameBacking", WithAlpha(GreyboxPalette.Paper, 0.85f), new Vector3(0f, NameY, 0f), new Vector2(2.1f, 0.34f), LabelBackingOrder);
            TMP_Text nameLabel = Label(root.transform, "Name", definition.displayName, 3.0f, GreyboxPalette.Ink, new Vector3(0f, NameY, 0f), new Vector2(2.4f, 0.4f), FontStyles.Bold);
            SpriteRenderer statusBacking = Sliced(root.transform, "StatusBacking", WithAlpha(GreyboxPalette.Paper, 0.85f), new Vector3(0f, StatusY, 0f), new Vector2(1.9f, 0.28f), LabelBackingOrder);
            TMP_Text status = Label(root.transform, "Status", string.Empty, 2.5f, GreyboxPalette.Warning, new Vector3(0f, StatusY, 0f), new Vector2(2.2f, 0.35f), FontStyles.Bold);

            var so = new SerializedObject(view);
            so.FindProperty("building").enumValueIndex = (int)definition.building;
            SerializedProperty artProperty = so.FindProperty("art");
            artProperty.arraySize = art.Count;
            for (int i = 0; i < art.Count; i++)
            {
                artProperty.GetArrayElementAtIndex(i).objectReferenceValue = art[i];
            }

            so.FindProperty("body").objectReferenceValue = plate;
            so.FindProperty("seatY").floatValue = PlateY;
            so.FindProperty("workerBadge").objectReferenceValue = badge;
            so.FindProperty("workerLabel").objectReferenceValue = workerLabel;
            so.FindProperty("nameLabel").objectReferenceValue = nameLabel;
            so.FindProperty("statusLabel").objectReferenceValue = status;
            so.FindProperty("statusBacking").objectReferenceValue = statusBacking;
            so.FindProperty("markersRoot").objectReferenceValue = markers;
            so.FindProperty("seatSprite").objectReferenceValue = GreyboxArtGenerator.Square;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Placeholder art per building (Kenney CC0 renders, see Art/Kenney).</summary>
        private static List<SpriteRenderer> BuildArt(Transform parent, BuildingId building)
        {
            var art = new List<SpriteRenderer>();
            switch (building)
            {
                case BuildingId.Reception:
                    art.Add(Art(parent, "Art", KenneyCity + "building-type-e.png", new Vector3(0f, ArtY, 0f), 2.1f, ArtOrder));
                    break;
                case BuildingId.Restaurant:
                    art.Add(Art(parent, "Art", KenneyCity + "building-type-q.png", new Vector3(0f, ArtY, 0f), 2.0f, ArtOrder));
                    break;
                case BuildingId.BeachBar:
                    art.Add(Art(parent, "Art", KenneyPirate + "structure-roof.png", new Vector3(0f, ArtY, 0f), 1.45f, ArtOrder));
                    break;
                default:
                    Debug.LogWarning($"No placeholder art for {building} yet; add a case in GreyboxSceneBuilder.BuildArt.");
                    break;
            }

            return art;
        }

        /// <summary>Where each building stands on the island (relative to its centre).</summary>
        private static Vector3 Offset(BuildingId building)
        {
            switch (building)
            {
                case BuildingId.Reception: return new Vector3(0f, 1.55f, 0f);
                case BuildingId.Restaurant: return new Vector3(-3.0f, 1.4f, 0f);
                case BuildingId.BeachBar: return new Vector3(1.9f, -1.9f, 0f);
                // Reserved spots for the later unlocks.
                case BuildingId.Pool: return new Vector3(3.0f, 1.4f, 0f);
                case BuildingId.Bungalows: return new Vector3(-2.0f, -1.9f, 0f);
                default: return Vector3.zero;
            }
        }

        /// <summary>A sprite from an asset path, scaled to <paramref name="width"/> world units.</summary>
        private static SpriteRenderer Art(Transform parent, string name, string spritePath, Vector3 position, float width, int order)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            if (sprite != null)
            {
                float scale = width / sprite.bounds.size.x;
                go.transform.localScale = new Vector3(scale, scale, 1f);
            }
            else
            {
                Debug.LogWarning($"Missing sprite {spritePath}");
            }

            return renderer;
        }

        private static SpriteRenderer Shape(Transform parent, string name, Sprite sprite, Color color, Vector3 position, Vector2 size, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = order;
            return renderer;
        }

        private static SpriteRenderer Sliced(Transform parent, string name, Color color, Vector3 position, Vector2 size, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = GreyboxArtGenerator.Rounded;
            renderer.drawMode = SpriteDrawMode.Sliced;
            renderer.size = size;
            renderer.color = color;
            renderer.sortingOrder = order;
            return renderer;
        }

        private static TMP_Text Label(Transform parent, string name, string text, float fontSize, Color color, Vector3 position, Vector2 size, FontStyles style)
        {
            // TextMeshPro resets its own RectTransform when added, so position a plain parent instead.
            var anchor = new GameObject(name);
            anchor.transform.SetParent(parent, false);
            anchor.transform.localPosition = position;

            var go = new GameObject("Text");
            go.transform.SetParent(anchor.transform, false);
            var label = go.AddComponent<TextMeshPro>();
            label.rectTransform.sizeDelta = size;
            label.text = text;
            label.fontSize = fontSize;
            label.color = color;
            label.fontStyle = style;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.sortingOrder = 10;
            return label;
        }

        private static Color WithAlpha(Color color, float alpha) => new Color(color.r, color.g, color.b, alpha);

        private static void SetReference(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetVector3(Object target, string field, Vector3 value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).vector3Value = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
