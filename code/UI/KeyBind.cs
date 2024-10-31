using Sandbox;
using Sandbox.UI;
using System;
using System.Linq;

namespace Instagib;

public class KeyBind : Label
{
    public InputAction Action { get; set; }
    bool Rebinding { get; set; }
    string TargetKey { get; set; }

    public KeyBind()
    {
        AcceptsFocus = true;
        BindClass("active", () => Rebinding);
        BindClass("with-highlight", () => !string.IsNullOrEmpty(TargetKey));
    }

    public override void Tick()
    {
        if (Rebinding && TargetKey == null)
        {
            Text = "PRESS A BUTTON";
            return;
        }

        if (TargetKey != null)
        {
            Text = TargetKey.ToUpperInvariant();
            return;
        }

        var str = IGameInstance.Current.GetBind(Action.Name, out var isDefault, out var isCommon);

        if (str == null)
        {
            str = Action.KeyboardCode;
            isDefault = true;
            Text = "";
            return;
        }

        str = str.ToUpperInvariant();

        if (isDefault)
        {
            Text = $"{str} (default)";
        }
        else
        {
            Text = str;
        }
    }

    protected override void OnMouseUp(MousePanelEvent e)
    {
        base.OnMouseUp(e);

        if (e.Button == "mouseleft")
        {
            Rebinding = true;
            TargetKey = null;

            IGameInstance.Current.TrapButtons((buttons) =>
            {
                if (buttons.Contains("ESCAPE", StringComparer.OrdinalIgnoreCase))
                {
                    Rebinding = false;
                    TargetKey = null;
                    CreateEvent("onchange");
                    return;
                }

                TargetKey = string.Join(" + ", buttons.OrderBy(x => x));
                Rebinding = false;
                CreateEvent("onchange");
            });
        }

        if (e.Button == "mouseright")
        {
            IGameInstance.Current.SetBind(Action.Name, null);
            TargetKey = null;
            CreateEvent("onchange");
        }
    }

    public void Apply()
    {
        if (string.IsNullOrEmpty(TargetKey))
            return;

        IGameInstance.Current.SetBind(Action.Name, TargetKey);
        TargetKey = null;
    }

    public void Cancel()
    {
        TargetKey = null;
    }
}
