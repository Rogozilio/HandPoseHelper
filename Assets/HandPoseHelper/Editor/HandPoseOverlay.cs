using System;
using System.Collections.Generic;
using System.IO;
using HandPoseHelper.Scripts.Enums;
using Scripts;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine;
using SaveDataTemplate = Scripts.SaveDataTemplate;

[Overlay(typeof(SceneView), "Hand Pose")]
public class HandPoseOverlay : IMGUIOverlay
{
    private static List<HandPoseOverlay> instances = new List<HandPoseOverlay>();

    private bool _isShowColorFingers;
    private bool _isSelectedSaveFile;

    private Texture2D _iconBone;
    private Texture2D _iconJoint;

    private string _nameSaveData;
    private string _namePose;

    private int _popupNamePose;

    private readonly float _widthLabel = 70f;
    private readonly float _widthField = 100f;
    private readonly float _widthButton = 80f;

    private Scripts.HandPoseHelper _handPoseHelper;
    private SaveDataTemplate _template;
    private AnimationClipTool _clipTool;

    private bool _isSelectedHand => _selectedHandType is HandType.Left or HandType.Right;
    private bool _isSelectedHands => _selectedHandType != HandType.None;

    private HandType _selectedHandType
    {
        get
        {
            var isLeftHand = false;
            var isRightHand = false;

            foreach (var obj in Selection.objects)
            {
                isLeftHand = isLeftHand || obj.name.Contains("Left");
                isRightHand = isRightHand || obj.name.Contains("Right");
            }

            HandType typeHand = HandType.None;

            if (isLeftHand && isRightHand)
                typeHand = HandType.All;
            else if (isLeftHand && !isRightHand)
                typeHand = HandType.Left;
            else if (!isLeftHand && isRightHand)
                typeHand = HandType.Right;

            return typeHand;
        }
    }

    private bool _isUseDataCollection => _template && _template.GetIsUseDataCollection;

    private bool _isNameFileExist
    {
        get
        {
            var template = AssetDatabase.LoadAssetAtPath<SaveDataTemplate>(_pathAsset);
            return template != null;
        }
    }

    private bool _isNamePoseExist
    {
        get
        {
            if (!_isUseDataCollection) return false;

            var template = AssetDatabase.LoadAssetAtPath<SaveDataTemplate>(_pathAsset);
            var name = template?.Load(_namePose?.Trim()).name;
            return name != null;
        }
    }

    private string _pathAsset => _handPoseHelper.saveDataSetting.path + "/" + _nameSaveData?.Trim() + ".asset";

    public override void OnCreated()
    {
        instances.Add(this);

        _iconBone = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/HandPoseHelper/Editor/Sprites/bone.png");
        _iconJoint = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/HandPoseHelper/Editor/Sprites/joint.png");

        _clipTool = new AnimationClipTool();
    }

    public override void OnWillBeDestroyed()
    {
        instances.Remove(this);
    }

    public static void DoWithInstances(Action<HandPoseOverlay> doWithInstance)
    {
        foreach (var instance in instances)
        {
            doWithInstance(instance);
        }
    }

    public override void OnGUI()
    {
        _handPoseHelper = GameObject.FindObjectOfType<Scripts.HandPoseHelper>();
        if (!_handPoseHelper) return;
        DrawHorizontalLine("Pose setting", 0f);
        HandSettings();
        DrawHorizontalLine("Save pose setting");
        SaveData();
        // DrawHorizontalLine("Create animation clip");
        // SaveDataInAnimationClip();
        DrawHorizontalLine("Color setting", 10f, 0f);
        EditorGUI.BeginChangeCheck();
        ChoiceColorFingers();
        if (EditorGUI.EndChangeCheck())
        {
            EditorUtility.SetDirty(_handPoseHelper);
        }
    }

    private void HandSettings()
    {
        EditorGUILayout.BeginHorizontal();

        GUI.enabled = _isSelectedHands;
        if (GUILayout.Button(_handPoseHelper.isDefaultPoseExist ? "Default Pose" : "Clear Pose",
                GUILayout.Width(_widthButton)))
        {
            _handPoseHelper.ClearOrDefaultPose(_selectedHandType);
        }

        GUI.enabled = true;

        GUI.enabled = _isSelectedHand;
        if (GUILayout.Button("Mirror Pose", GUILayout.Width(_widthButton)))
        {
            var isLeftHand = (bool)Selection.activeGameObject?.name.Contains("Left");
            _handPoseHelper.MirrorPose(isLeftHand);
        }

        GUI.enabled = true;

        GUI.enabled = _isSelectedHands;
        if (GUILayout.Button("Auto Pose", GUILayout.Width(_widthButton)))
        {
            _handPoseHelper.AutoPose();
        }

        GUI.enabled = true;

        EditorGUILayout.EndHorizontal();
    }

    private void SaveData()
    {
        if (!_template) CreateSO(_handPoseHelper);

        if (Selection.activeObject?.GetType().BaseType == typeof(SaveDataTemplate))
        {
            _isSelectedSaveFile = true;
            _nameSaveData = Selection.activeObject?.name;
            FindTemplateByName(Selection.activeObject?.GetType().ToString());
        }
        else if (_isSelectedSaveFile)
        {
            _isSelectedSaveFile = false;
            _nameSaveData = string.Empty;
            _namePose = string.Empty;
        }

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Template: ", GUILayout.Width(_widthLabel));
        GUI.enabled = !_isSelectedSaveFile;
        EditorGUI.BeginChangeCheck();
        _handPoseHelper.popupIndexTemplate = EditorGUILayout.Popup(_handPoseHelper.popupIndexTemplate,
            _handPoseHelper.saveDataSetting.names.ToArray()
            , GUILayout.Width(_widthField));
        if (EditorGUI.EndChangeCheck())
        {
            CreateSO(_handPoseHelper);
            _nameSaveData = string.Empty;
            _namePose = string.Empty;
        }

        GUI.enabled = true;

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Name file:", GUILayout.Width(_widthLabel));
        GUI.enabled = !_isSelectedSaveFile;
        var names = GetFilenameByType(_handPoseHelper.saveDataSetting.templates[_handPoseHelper.popupIndexTemplate]
            .GetClass());
        if (names.Count > 0)
        {
            _nameSaveData = EditorGUILayout.TextField(_nameSaveData, GUILayout.Width(_widthField - 18));
            _nameSaveData = DrawDropdown(GUILayoutUtility.GetLastRect(), GUIContent.none, names);
        }
        else
        {
            _nameSaveData = EditorGUILayout.TextField(_nameSaveData, GUILayout.Width(_widthField));
        }

        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();

        if (_isUseDataCollection)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Name pose:", GUILayout.Width(_widthLabel));
            var template = AssetDatabase.LoadAssetAtPath<SaveDataTemplate>(_pathAsset);

            if ((_isSelectedSaveFile || _isNameFileExist) && template.GetAllNames?.Count > 0)
            {
                _namePose = EditorGUILayout.TextField(_namePose, GUILayout.Width(_widthField - 18));
                _namePose = DrawDropdown(GUILayoutUtility.GetLastRect(), GUIContent.none, template.GetAllNames, false);
            }
            else
            {
                _namePose = EditorGUILayout.TextField(_namePose, GUILayout.Width(_widthField));
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();
        ShowButtonLoad();

        if (_isSelectedSaveFile || _isNameFileExist) ShowButtonOverwrite();
        else ShowButtonCreate();

        if (_isNamePoseExist) ShowButtonReplace();
        else ShowButtonAdd();
        EditorGUILayout.EndHorizontal();
    }

    private void SaveDataInAnimationClip()
    {
        GUI.enabled = _isNameFileExist && !_isUseDataCollection || _isNamePoseExist;
        EditorGUILayout.BeginHorizontal();
        ShowButtonCreateClipLeftHand();
        ShowButtonCreateClipBothHand();
        ShowButtonCreateClipRightHand();
        EditorGUILayout.EndHorizontal();
        GUI.enabled = true;
    }

    private List<string> GetFilenameByType(Type type)
    {
        var guids = AssetDatabase.FindAssets("t:" + type, new[] { _handPoseHelper.saveDataSetting.path });

        var result = new List<string>();
        foreach (var guid in guids)
        {
            result.Add(Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid)));
        }

        return result;
    }

    private string DrawDropdown(Rect rect, GUIContent label, List<string> allNames, bool isNameFile = true)
    {
        var newRect = new Rect(rect.x + rect.size.x - 2, rect.y, 20, 20);
        if (!EditorGUI.DropdownButton(newRect, label, FocusType.Passive))
        {
            return isNameFile ? _nameSaveData : _namePose;
        }

        void handleItemClicked(object parameter)
        {
            if (isNameFile)
                _nameSaveData = parameter.ToString();
            else
                _namePose = parameter.ToString();
        }

        GUI.FocusControl(null);
        GenericMenu menu = new GenericMenu();
        foreach (var name in allNames)
        {
            menu.AddItem(new GUIContent(name), false, handleItemClicked, name);
        }

        menu.DropDown(rect);
        return isNameFile ? _nameSaveData : _namePose;
    }

    private void DrawHorizontalLine(string text = "", float topSpace = 10f, float bottomSpace = 2f)
    {
        GUILayout.Space(topSpace);

        if (text != string.Empty)
        {
            var widthText = EditorStyles.label.CalcSize(new GUIContent(text)).x;
            var widthLine = (size.x - widthText) / 2;
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider, GUILayout.Width(widthLine - 10));
            EditorGUILayout.LabelField(text, GUILayout.Width(widthText));
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider, GUILayout.Width(widthLine - 10));
            EditorGUILayout.EndHorizontal();
        }
        else
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

        GUILayout.Space(bottomSpace);
    }

    private void CreateSO(Scripts.HandPoseHelper handPoseHelper)
    {
        var type = handPoseHelper.saveDataSetting.templates[handPoseHelper.popupIndexTemplate].GetClass();
        _template = (SaveDataTemplate)ScriptableObject.CreateInstance(type);
    }

    private void FindTemplateByName(string nowName)
    {
        for (var i = 0; i < _handPoseHelper.saveDataSetting.templates.Count; i++)
        {
            if (nowName == _handPoseHelper.saveDataSetting.templates[i].name)
            {
                _handPoseHelper.popupIndexTemplate = i;
                CreateSO(_handPoseHelper);
                return;
            }
        }
    }

    private void ShowButtonLoad()
    {
        GUI.enabled = (_isSelectedSaveFile || _isNameFileExist) &&
                      (!_isUseDataCollection || (_isUseDataCollection && _isNamePoseExist));

        if (GUILayout.Button("Load", GUILayout.Width(_widthButton)))
        {
            var template = AssetDatabase.LoadAssetAtPath<SaveDataTemplate>(_pathAsset);
            var handPose = _isNamePoseExist ? template.Load(_namePose) : template.Load();

            if (handPose.leftHand.joints.Count != _handPoseHelper.leftHandInfo.values.Count ||
                handPose.rightHand.joints.Count != _handPoseHelper.rightHandInfo.values.Count)
            {
                Debug.LogError("Load failed. Save data do not match choice hands.");
                return;
            }

            _handPoseHelper.SetHandPoseData(handPose);
        }

        GUI.enabled = true;
    }

    private void ShowButtonCreate()
    {
        if (GUILayout.Button("Create", GUILayout.Width(_widthButton)))
        {
            if (_handPoseHelper.saveDataSetting.path == string.Empty)
            {
                Debug.LogError("Save data path empty. Set path in prefab HandPoseHelper in setting save data.");
                return;
            }

            var type = _handPoseHelper.saveDataSetting.templates[_handPoseHelper.popupIndexTemplate].GetClass();
            if (type.BaseType != typeof(SaveDataTemplate))
            {
                Debug.LogError("Script " + type + " must inherit from SaveDataTemplate.");
                return;
            }

            _template.Save(_handPoseHelper.GetHandPoseData(),
                _namePose != string.Empty ? _namePose?.Trim() : _nameSaveData?.Trim());
            AssetDatabase.CreateAsset(_template, _pathAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.FocusProjectWindow();
            _template = null;
        }
    }

    private void ShowButtonOverwrite()
    {
        if (GUILayout.Button("Overwrite", GUILayout.Width(_widthButton)))
        {
            var template = AssetDatabase.LoadAssetAtPath<SaveDataTemplate>(_pathAsset);
            if (_isUseDataCollection)
                template.Save(_handPoseHelper.GetHandPoseData(),
                    _namePose != string.Empty ? _namePose?.Trim() : _nameSaveData?.Trim());
            else
                template.Save(_handPoseHelper.GetHandPoseData(),
                    _isNamePoseExist ? _namePose?.Trim() : _nameSaveData?.Trim());

            EditorUtility.SetDirty(template);
        }
    }

    private void ShowButtonCreateClipLeftHand()
    {
        if (GUILayout.Button("Left", GUILayout.Width(_widthButton)))
        {
            _clipTool.CreateClip(_namePose + "_L", _handPoseHelper.leftHandPrefab,
                _handPoseHelper.leftHandInfo.values);
        }
    }

    private void ShowButtonCreateClipBothHand()
    {
        if (GUILayout.Button("Both", GUILayout.Width(_widthButton)))
        {
            var name = _isUseDataCollection ? _namePose : _nameSaveData;
            _clipTool.CreateClip(name + "_L", _handPoseHelper.leftHandPrefab,
                _handPoseHelper.leftHandInfo.values);
            _clipTool.CreateClip(name + "_R", _handPoseHelper.rightHandPrefab,
                _handPoseHelper.rightHandInfo.values);
        }
    }

    private void ShowButtonCreateClipRightHand()
    {
        if (GUILayout.Button("Right", GUILayout.Width(_widthButton)))
        {
            _clipTool.CreateClip(_namePose + "_R", _handPoseHelper.rightHandPrefab,
                _handPoseHelper.rightHandInfo.values);
        }
    }

    private void ShowButtonAdd()
    {
        GUI.enabled = _isNameFileExist && _isUseDataCollection && !_isNamePoseExist &&
                      _namePose?.Trim() != string.Empty;
        if (GUILayout.Button("Add", GUILayout.Width(_widthButton)))
        {
            var template = AssetDatabase.LoadAssetAtPath<SaveDataTemplate>(_pathAsset);
            template.SaveElement(_handPoseHelper.GetHandPoseData(), _namePose);
            EditorUtility.SetDirty(template);
        }

        GUI.enabled = true;
    }

    private void ShowButtonReplace()
    {
        GUI.enabled = (_isSelectedSaveFile || _isNameFileExist) && _isUseDataCollection;
        if (GUILayout.Button("Replace", GUILayout.Width(_widthButton)))
        {
            var template = AssetDatabase.LoadAssetAtPath<SaveDataTemplate>(_pathAsset);
            template.SaveElement(_handPoseHelper.GetHandPoseData(), _namePose);
            EditorUtility.SetDirty(template);
        }

        GUI.enabled = true;
    }

    private void ChoiceColorFingers()
    {
        if (_handPoseHelper.colorFingers.bones == null || _handPoseHelper.colorFingers.bones.Count == 0) return;
        if (_handPoseHelper.colorFingers.joints == null || _handPoseHelper.colorFingers.joints.Count == 0) return;

        _isShowColorFingers = EditorGUILayout.Foldout(_isShowColorFingers, "Color fingers", true);

        if (!_isShowColorFingers) return;

        _handPoseHelper.colorFingers.isForEachFinger =
            GUILayout.Toggle(_handPoseHelper.colorFingers.isForEachFinger, "For Each Finger");
        if (_handPoseHelper.colorFingers.isForEachFinger)
        {
            for (var i = 0; i < _handPoseHelper.colorFingers.bones.Count; i++)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Color finger " + (i + 1) + ": ");
                GUILayout.Label(_iconBone, GUILayout.Width(17), GUILayout.Height(17));
                GUILayout.Label("-");
                _handPoseHelper.colorFingers.bones[i] =
                    EditorGUILayout.ColorField(new GUIContent(), _handPoseHelper.colorFingers.bones[i], false, true,
                        false,
                        GUILayout.Width(40));
                GUILayout.Label("|");
                GUILayout.Label(_iconJoint, GUILayout.Width(17), GUILayout.Height(17));
                GUILayout.Label("-");
                _handPoseHelper.colorFingers.joints[i] =
                    EditorGUILayout.ColorField(new GUIContent(), _handPoseHelper.colorFingers.joints[i], false, true,
                        false,
                        GUILayout.Width(40));
                GUILayout.EndHorizontal();
            }
        }
        else
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Color fingers: ");
            GUILayout.Label(_iconBone, GUILayout.Width(17), GUILayout.Height(17));
            GUILayout.Label("-");
            _handPoseHelper.colorFingers.bones[0] =
                EditorGUILayout.ColorField(new GUIContent(), _handPoseHelper.colorFingers.bones[0], false, true, false,
                    GUILayout.Width(40));
            GUILayout.Label("|");
            GUILayout.Label(_iconJoint, GUILayout.Width(17), GUILayout.Height(17));
            GUILayout.Label("-");
            _handPoseHelper.colorFingers.joints[0] =
                EditorGUILayout.ColorField(new GUIContent(), _handPoseHelper.colorFingers.joints[0], false, true, false,
                    GUILayout.Width(40));
            GUILayout.EndHorizontal();
        }
    }
}