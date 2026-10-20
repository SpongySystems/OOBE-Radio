using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace OobeRadio.Gui;

public sealed class RadioDialog(ICoreClientAPI api, OobeRadioSystem radio) : GuiDialog(api)
{
    public override string ToggleKeyCombinationCode => "ooberadio";

    public void OpenPlayer()
    {
        if (IsOpened()) { TryClose(); return; }
        ComposePlayer();
        TryOpen();
    }

    private void ComposePlayer()
    {
        SingleComposer?.Dispose();
        var background = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
        background.BothSizing = ElementSizing.FitToChildren;
        var dialog = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);

        SingleComposer = capi.Gui.CreateCompo("ooberadio-player", dialog)
            .AddShadedDialogBG(background)
            .AddDialogTitleBar("Out of Bounds Radio", () => TryClose())
            .BeginChildElements(background)
            .AddStaticElement(new StationLogo(capi, ElementBounds.Fixed(90, 35, 250, 250)))
            .AddStaticText("OUT OF BOUNDS", CairoFont.WhiteMediumText(),
                EnumTextOrientation.Center, ElementBounds.Fixed(0, 296, 430, 30))
            .AddDynamicText(radio.StatusText, CairoFont.WhiteSmallText(),
                ElementBounds.Fixed(0, 335, 430, 50), "status")
            .AddButton("Play", radio.PlayFromUi, ElementBounds.Fixed(20, 393, 180, 38), key: "play")
            .AddButton("Stop", radio.StopFromUi, ElementBounds.Fixed(230, 393, 180, 38), key: "stop")
            .AddStaticText("Radio volume", CairoFont.WhiteSmallText(), ElementBounds.Fixed(20, 445, 125, 24))
            .AddSlider(radio.SetVolume, ElementBounds.Fixed(150, 442, 260, 26), "volume")
            .AddStaticText("Live radio • Listen anywhere", CairoFont.WhiteDetailText(),
                EnumTextOrientation.Center, ElementBounds.Fixed(0, 482, 430, 24))
            .EndChildElements()
            .Compose();
        SingleComposer.GetSlider("volume").SetValues(radio.VolumePercent, 0, 100, 1, "%");
        Refresh();
    }

    public void Refresh()
    {
        if (SingleComposer == null) return;
        SingleComposer.GetDynamicText("status").SetNewText(radio.StatusText);
        SingleComposer.GetButton("play").Enabled = radio.CanPlay;
        SingleComposer.GetButton("stop").Enabled = radio.IsRequested;
    }
}

// Scale the original transparent PNG with GUI scale; the built-in AddImage
// helper paints at the source's native pixel size in some game versions.
internal sealed class StationLogo(ICoreClientAPI api, ElementBounds bounds) : GuiElement(api, bounds)
{
    public override void ComposeElements(Context context, ImageSurface surface)
    {
        using var logo = getImageSurfaceFromAsset(api, new AssetLocation("ooberadio:textures/gui/logo.png"));
        context.Save();
        context.Translate(Bounds.drawX, Bounds.drawY);
        context.Scale(Bounds.InnerWidth / logo.Width, Bounds.InnerHeight / logo.Height);
        context.SetSourceSurface(logo, 0, 0);
        context.Paint();
        context.Restore();
    }
}
