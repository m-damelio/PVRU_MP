using UnityEngine;
using UnityEngine.Events;
using Fusion;
using System.Linq;
using Meta.WitAi;
using Unity.VisualScripting;

public class ActivateMirrorButton : NetworkBehaviour
{
    public ColorMirror mirror1;
    public ColorMirror mirror2;
    public ColorMirror mirror3;
    public ColorMirror mirror4;
    public ColorMirror mirror5;
    public ColorMirror mirror6;

    public bool testBool = false;
    public bool firstPressedP1 = false;
    public bool firstPressedP2 = false;
    public bool firstPressedP3 = false;

    public override void Spawned()
    {
        SetInitialColor();
    }
    public void ToggleInactivePair1()
    {
        if (!firstPressedP1)
        {
            mirror3.SetInactiveState(true);
            mirror2.SetInactiveState(false);
            firstPressedP1 = true;
        }
        else
        {
            mirror3.SetInactiveState(false);
            mirror2.SetInactiveState(true);
            firstPressedP1 = false;
        }
            

    }
    public void ToggleInactivePair2()
    {
        if (!firstPressedP2)
        {
            mirror5.SetInactiveState(true);
            mirror1.SetInactiveState(false);
            firstPressedP2 = true;
        }
        else
        {
            mirror5.SetInactiveState(false);
            mirror1.SetInactiveState(true);
            firstPressedP2 = false;
        }
           
    }
    public void ToggleInactivePair3()
    {
        if (!firstPressedP3)
        {
            mirror4.SetInactiveState(true);
            mirror6.SetInactiveState(false);
            firstPressedP3 = true;
        }
        else
        {
            mirror4.SetInactiveState(false);
            mirror6.SetInactiveState(true);
            firstPressedP3 = false;
        }
    }

    public void SetInitialColor()
    {
        if(mirror6 == null)
        {
            return;
        }
        else
        {
            mirror2.SetInactiveState(true);
            mirror1.SetInactiveState(true);
            mirror6.SetInactiveState(true);
        }
    }


}
