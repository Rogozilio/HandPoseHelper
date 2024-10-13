using System.Collections;
using System.Collections.Generic;
using Scripts;
using UnityEngine;

public class DefaultTemplate : SaveDataTemplate
{
    [SerializeField] private HandPoseData _hands;

    public override void Save(HandPoseData hands, string name)
    {
        hands.name = name;
        _hands = hands;
    }

    public override HandPoseData Load()
    {
        return _hands;
    }
}
