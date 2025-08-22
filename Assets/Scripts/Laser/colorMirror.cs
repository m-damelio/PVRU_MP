using UnityEngine;
using System.Collections;
using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;


public class ColorMirror : NetworkBehaviour
{
    [Networked] public bool IsInactive { get; set; }
    public bool inactive = false;
    public bool isSelected = false;

    private Renderer cachedRenderer;

    private static readonly Color inactiveColor = new Color(1f, 0f, 0f, 1f); // rot
    public Color activeColor;

    private ChangeDetector _changeDetector;


    public override void Spawned()
    {
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
        cachedRenderer = transform.GetChild(0).GetComponentInChildren<Renderer>();
        activeColor = cachedRenderer.material.color;
        UpdateColor();
    }


    public override void Render()
    {
        foreach ( var changedProperty in _changeDetector.DetectChanges(this))
        {
            if(changedProperty == nameof(IsInactive))
            {
                // Wird bei jedem Frame-Update auf dem Client aufgerufen
                UpdateColor();
            }
        }
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