using System.Collections;
using System.Collections.Generic;
using Scripts;
using UnityEngine;

public class DefaultTemplate : SaveDataTemplate
{
    public HandPoseData hands;

    public override void Save(HandPoseData hands, string name)
    {
        hands.name = name;
        this.hands = hands;
    }

    public override HandPoseData Load()
    {
        return hands;
    }
}
