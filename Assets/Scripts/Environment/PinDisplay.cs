using UnityEngine;
using Fusion;
using TMPro;
using System.Collections;

public class PinDisplay : NetworkBehaviour
{
    [Header("PIN Settings")]
    [SerializeField] private int[] correctPin = new int[4] { 1, 2, 3, 4 };

    [Header("UI References")]
    [SerializeField] private TextMeshPro[] pinDigits;

    [Header("Colors")]
    [SerializeField] private Color normalColor = Color.white;

    [Header("Settings")]
    [SerializeField] private float autoHideDelay = 5f;

    [Networked] public bool IsPinVisible { get; set; }

    private Coroutine autoHideCoroutine;

    public override void Spawned()
    {
        InitializePinDisplay();
    }

    private void InitializePinDisplay()
    {
        foreach (var digit in pinDigits)
        {
            if (digit != null)
            {
                digit.text = "";
                digit.color = normalColor;
            }
        }
    }

    public void TogglePinDisplay()
    {
        if (Object.HasStateAuthority)
        {
            IsPinVisible = !IsPinVisible;

            if (IsPinVisible)
            {
                if (autoHideCoroutine != null) StopCoroutine(autoHideCoroutine);
                autoHideCoroutine = StartCoroutine(AutoHideAfterDelay());
            }
            else
            {
                if (autoHideCoroutine != null)
                {
                    StopCoroutine(autoHideCoroutine);
                    autoHideCoroutine = null;
                }
            }

            RPC_UpdateDisplay();
        }
    }

    private IEnumerator AutoHideAfterDelay()
    {
        yield return new WaitForSeconds(autoHideDelay);
        if (IsPinVisible)
        {
            IsPinVisible = false;
            RPC_UpdateDisplay();
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_UpdateDisplay()
    {
        if (IsPinVisible)
        {
            for (int i = 0; i < pinDigits.Length; i++)
            {
                if (pinDigits[i] != null)
                {
                    pinDigits[i].text = correctPin[i].ToString();
                    pinDigits[i].color = normalColor;
                }
            }
        }
        else
        {
            InitializePinDisplay();
        }
    }

    [ContextMenu("Toggle PIN (Debug)")]
    private void Debug_TogglePin() => TogglePinDisplay();
}
