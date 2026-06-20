using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace VRChopping
{
    public class AfterTreeFallTutorial : MonoBehaviour
    {
        private enum TutorialPhase
        {
            HealDeadTrees,
            GrowTrees,
            Complete
        }

        [Header("Objectives")]
        [SerializeField] private int deadTreesRequired = 3;
        [SerializeField] private int growingTreesRequired = 3;

        [Header("Scene Objects")]
        [SerializeField] private TreeLife[] deadTrees;
        [SerializeField] private GrowingTree[] growingTrees;
        [SerializeField] private EnergyBall[] energyBalls;

        [Header("HUD")]
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private Vector3 hudLocalPosition = new Vector3(0f, 0.22f, 0.9f);
        [SerializeField] private Vector2 hudPanelSize = new Vector2(720f, 220f);
        [SerializeField] private float hudScale = 0.0012f;

        [Header("Copy")]
        [SerializeField] private string healTitle = "Objective: Heal Dead Trees";
        [SerializeField] private string healInstructions =
            "Point at a dead tree and hold the trigger until it heals.";
        [SerializeField] private string growTitle = "Objective: Grow Trees";
        [SerializeField] private string growInstructions =
            "Grab an energy ball and throw it into a growing tree.";
        [SerializeField] private string completeMessage = "All objectives complete! Well done.";

        private TutorialPhase _phase = TutorialPhase.HealDeadTrees;
        private int _healedCount;
        private int _grownCount;

        private readonly List<TreeLife> _trackedDeadTrees = new List<TreeLife>();
        private readonly List<GrowingTree> _trackedGrowingTrees = new List<GrowingTree>();

        private Transform _hudRoot;
        private TextMeshProUGUI _titleText;
        private TextMeshProUGUI _progressText;
        private TextMeshProUGUI _instructionsText;

        private void Start()
        {
            ResolveCamera();
            BuildHud();
            SetupHealPhase();
            RefreshHud();
        }

        private void LateUpdate()
        {
            if (_hudRoot == null || cameraTransform == null)
                return;

            _hudRoot.position = cameraTransform.TransformPoint(hudLocalPosition);
            var flatForward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up);
            if (flatForward.sqrMagnitude > 0.0001f)
                _hudRoot.rotation = Quaternion.LookRotation(flatForward, Vector3.up);
        }

        private void ResolveCamera()
        {
            if (cameraTransform != null)
                return;

            var mainCamera = Camera.main;
            if (mainCamera != null)
            {
                cameraTransform = mainCamera.transform;
                return;
            }

            var xrCamera = FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
            if (xrCamera != null && xrCamera.Camera != null)
                cameraTransform = xrCamera.Camera.transform;
        }

        private void BuildHud()
        {
            var hudObject = new GameObject("AfterTreeFall Tutorial HUD");
            _hudRoot = hudObject.transform;

            var canvasObject = new GameObject("Canvas");
            canvasObject.transform.SetParent(_hudRoot, false);

            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 10f;

            canvasObject.AddComponent<TrackedDeviceGraphicRaycaster>();

            var canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.sizeDelta = hudPanelSize;
            canvasRect.localScale = Vector3.one * hudScale;

            var panelObject = new GameObject("Panel");
            panelObject.transform.SetParent(canvasObject.transform, false);
            var panelRect = panelObject.AddComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            var panelImage = panelObject.AddComponent<Image>();
            panelImage.color = new Color(0f, 0f, 0f, 0.72f);

            _titleText = CreateStretchedText(
                panelObject.transform,
                "Title",
                34,
                FontStyles.Bold,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -14f),
                new Vector2(-28f, 44f));

            _progressText = CreateStretchedText(
                panelObject.transform,
                "Progress",
                32,
                FontStyles.Bold,
                new Vector2(0f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, 10f),
                new Vector2(-28f, 40f));

            _instructionsText = CreateStretchedText(
                panelObject.transform,
                "Instructions",
                24,
                FontStyles.Italic,
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 16f),
                new Vector2(-28f, 72f));
            _instructionsText.color = new Color(0.85f, 0.92f, 1f, 1f);
        }

        private static TextMeshProUGUI CreateStretchedText(
            Transform parent,
            string name,
            float fontSize,
            FontStyles fontStyle,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            var textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);

            var rect = textObject.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;

            var text = textObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = true;
            text.color = Color.white;
            text.text = string.Empty;
            return text;
        }

        private void SetupHealPhase()
        {
            _phase = TutorialPhase.HealDeadTrees;
            _healedCount = 0;
            _grownCount = 0;

            CollectDeadTrees();
            CacheGrowingTrees();
            RegisterDeadTrees();
        }

        private void SetupGrowPhase()
        {
            _phase = TutorialPhase.GrowTrees;
            RegisterGrowingTrees();
            RefreshHud();
        }

        private void CollectDeadTrees()
        {
            _trackedDeadTrees.Clear();

            foreach (var tree in ResolveDeadTrees())
            {
                if (_trackedDeadTrees.Count >= deadTreesRequired)
                    break;

                tree.gameObject.SetActive(true);
                _trackedDeadTrees.Add(tree);
            }
        }

        private void CacheGrowingTrees()
        {
            _trackedGrowingTrees.Clear();

            foreach (var tree in ResolveGrowingTrees())
            {
                if (_trackedGrowingTrees.Count >= growingTreesRequired || tree.IsGrown)
                    continue;

                tree.gameObject.SetActive(true);
                _trackedGrowingTrees.Add(tree);
            }
        }

        private TreeLife[] ResolveDeadTrees()
        {
            if (deadTrees != null && deadTrees.Length > 0)
                return deadTrees;

            return FilterNumberedSceneObjects(
                FindObjectsByType<TreeLife>(FindObjectsInactive.Include, FindObjectsSortMode.None));
        }

        private GrowingTree[] ResolveGrowingTrees()
        {
            if (growingTrees != null && growingTrees.Length > 0)
                return growingTrees;

            return FilterNumberedSceneObjects(
                FindObjectsByType<GrowingTree>(FindObjectsInactive.Include, FindObjectsSortMode.None));
        }

        private EnergyBall[] ResolveEnergyBalls()
        {
            if (energyBalls != null && energyBalls.Length > 0)
                return energyBalls;

            return FilterNumberedSceneObjects(
                FindObjectsByType<EnergyBall>(FindObjectsInactive.Include, FindObjectsSortMode.None));
        }

        private static T[] FilterNumberedSceneObjects<T>(T[] components) where T : Component
        {
            var filtered = new List<T>();
            foreach (var component in components)
            {
                if (component != null && component.gameObject.name.Contains("("))
                    filtered.Add(component);
            }

            return filtered.ToArray();
        }

        private void RegisterDeadTrees()
        {
            foreach (var tree in _trackedDeadTrees)
            {
                if (tree == null)
                    continue;

                tree.TreeHealed -= OnTreeHealed;
                if (!tree.IsHealed)
                    tree.TreeHealed += OnTreeHealed;
                else
                    _healedCount++;
            }

            if (_healedCount >= deadTreesRequired)
                SetupGrowPhase();
        }

        private void RegisterGrowingTrees()
        {
            foreach (var tree in _trackedGrowingTrees)
            {
                if (tree == null)
                    continue;

                tree.TreeGrown -= OnTreeGrown;
                if (!tree.IsGrown)
                    tree.TreeGrown += OnTreeGrown;
                else
                    _grownCount++;
            }

            if (_grownCount >= growingTreesRequired)
                CompleteTutorial();
        }

        private void OnTreeHealed()
        {
            if (_phase != TutorialPhase.HealDeadTrees)
                return;

            _healedCount = CountHealedTrees();
            RefreshHud();

            if (_healedCount >= deadTreesRequired)
                SetupGrowPhase();
        }

        private void OnTreeGrown()
        {
            if (_phase != TutorialPhase.GrowTrees)
                return;

            _grownCount = CountGrownTrees();
            RefreshHud();

            if (_grownCount >= growingTreesRequired)
                CompleteTutorial();
        }

        private int CountHealedTrees()
        {
            var count = 0;
            foreach (var tree in _trackedDeadTrees)
            {
                if (tree != null && tree.IsHealed)
                    count++;
            }

            return count;
        }

        private int CountGrownTrees()
        {
            var count = 0;
            foreach (var tree in _trackedGrowingTrees)
            {
                if (tree != null && tree.IsGrown)
                    count++;
            }

            return count;
        }

        private void CompleteTutorial()
        {
            _phase = TutorialPhase.Complete;
            RefreshHud();
        }

        private void RefreshHud()
        {
            if (_titleText == null)
                return;

            switch (_phase)
            {
                case TutorialPhase.HealDeadTrees:
                    _titleText.text = healTitle;
                    _progressText.text = $"{_healedCount} / {deadTreesRequired}";
                    _instructionsText.text = healInstructions;
                    break;

                case TutorialPhase.GrowTrees:
                    _titleText.text = growTitle;
                    _progressText.text = $"{_grownCount} / {growingTreesRequired}";
                    _instructionsText.text = growInstructions;
                    break;

                default:
                    _titleText.text = completeMessage;
                    _progressText.text = string.Empty;
                    _instructionsText.text = string.Empty;
                    break;
            }
        }
    }
}
