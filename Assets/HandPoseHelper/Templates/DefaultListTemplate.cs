using System.Collections;
using System.Collections.Generic;
using Scripts;
using UnityEngine;

public class DefaultListTemplate : SaveDataTemplate
{
    [SerializeField] private List<HandPoseData> _hands;

    public override List<string> GetAllNames
    {
        get
        {
            var names = new List<string>();
            foreach (var value in _hands)
            {
                names.Add(value.name);
            }

            return names;
        }
    }

    protected override void OnEnable()
    {
        IsUseDataCollection = true;

        _hands ??= new List<HandPoseData>();
    }

    public override void Save(HandPoseData hands, string name)
    {
        hands.name = name;
        _hands.Clear();
        _hands.Add(hands);
    }

    public override void SaveElement(HandPoseData hands, string name)
    {
        hands.name = name;
        for (var i = 0; i < _hands.Count; i++)
        {
            if (_hands[i].name == hands.name)
            {
                _hands[i] = hands;
                return;
            }
        }
        
        _hands.Add(hands);
    }

    // public override bool FindByName(string name, out HandPoseData handPoseData)
    // {
    //     foreach (var data in _hands)
    //     {
    //         if (data.name == name)
    //         {
    //             handPoseData = data;
    //             return true;
    //         }
    //     }
    //
    //     handPoseData = new HandPoseData();
    //     return false;
    // }
    public override HandPoseData Load(string name)
    {
        foreach (var data in _hands)
        {
            if (data.name == name)
            {
                return data;
            }
        }
        
        return new HandPoseData();
    }
}
