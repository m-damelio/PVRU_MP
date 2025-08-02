using UnityEngine;
using System.Collections;
using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Oculus.Interaction;


public class ColorMirror : NetworkBehaviour
{
    [Networked] private bool IsInactive { get; set; }
    public bool inactive = false;
    public bool isSelected = false;

    private Renderer cachedRenderer;

    private static readonly Color inactiveColor = new Color(1f, 0f, 0f, 1f); // rot
    public Color activeColor;


    public override void Spawned()
    {
        cachedRenderer = transform.GetChild(0).GetComponentInChildren<Renderer>();
        activeColor = cachedRenderer.material.color;
        UpdateColor();
    }


    public override void Render()
    {
        // Wird bei jedem Frame-Update auf dem Client aufgerufen
        UpdateColor();
    }

    private void UpdateColor()
    {
        if (cachedRenderer != null)
        {
            if (!isSelected)
            {
                cachedRenderer.material.color = IsInactive ? inactiveColor : activeColor;
            }
            
        }


    }

    // Methode zum Ändern des Status – darf nur vom StateAuthority-Client aufgerufen werden
    public void SetInactiveState(bool state)
    {
        if (HasStateAuthority)
        {
            IsInactive = state;
            inactive = state;
        }
    }


}