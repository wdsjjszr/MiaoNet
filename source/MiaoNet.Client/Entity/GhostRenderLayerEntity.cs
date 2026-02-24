using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;
using Monocle;

namespace Celeste.Mod.MiaoNet;

public sealed class GhostRenderLayerEntity : MiaoNetEntity
{
    // 定义 Max Blend 混合状态
    // 这会取源像素和目标像素中颜色/Alpha值的最大值，从而避免叠加变暗
    private static readonly BlendState MaxBlendState = new()
    {
        ColorBlendFunction = BlendFunction.Add,
        AlphaBlendFunction = BlendFunction.Max,
        ColorSourceBlend = Blend.One,
        ColorDestinationBlend = Blend.InverseSourceAlpha,
        AlphaSourceBlend = Blend.One,
        AlphaDestinationBlend = Blend.One
    };

    public static bool UseCustomPipeline = true;

    [Command("miaonet_toggle_blend", "Toggle ghost rendering blend mode")]
    public static void ToggleBlend()
    {
        UseCustomPipeline = !UseCustomPipeline;
        Engine.Commands.Log($"Ghost Blend Mode: {(UseCustomPipeline ? "MaxBlend (Custom)" : "AlphaBlend (Default)")}");
    }

    // 缓存比较器以避免每帧分配委托 (Zero GC)
    private static readonly Comparison<MiaoNetGhostEntity> RenderSortComparison = (a, b) =>
    {
        // 按 Depth 降序排序 (Depth 越大越靠后，先绘制)
        return b.Depth - a.Depth;
    };

    private readonly bool isHigh;
    // 缓存列表以避免每帧分配内存
    private readonly List<MiaoNetGhostEntity> renderList = new();

    public GhostRenderLayerEntity(bool isHigh)
    {
        Tag = MiaoNetTag.Tag;
        Depth = isHigh ? Depths.Top : (Depths.Player + 1);
        this.isHigh = isHigh;
    }

    public override void Render()
    {
        var gd = Engine.Instance.GraphicsDevice;
        Level level = SceneAs<Level>();

        GameplayRenderer.End();

        gd.SetRenderTarget(GameplayBuffers.TempA);
        gd.Clear(Color.Transparent);

        // 使用自定义的 MaxBlendState 或默认 AlphaBlend 开启 SpriteBatch
        BlendState blendState = UseCustomPipeline ? MaxBlendState : BlendState.AlphaBlend;
        Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, blendState, SamplerState.PointWrap, DepthStencilState.None, RasterizerState.CullNone, null, level.Camera.Matrix);

        renderList.Clear();
        foreach (var entity in level.Tracker.GetEntities<MiaoNetGhostEntity>())
        {
            var ghostEntity = (MiaoNetGhostEntity)entity;
            if (isHigh ? ghostEntity.Depth <= Depth : ghostEntity.Depth >= Depth)
                renderList.Add(ghostEntity);
        }

        // 按Depth排序
        renderList.Sort(RenderSortComparison);

        // 按顺序渲染
        foreach (var entity in renderList)
        {
            entity.GhostRender();
        }

        GameplayRenderer.End();

        gd.SetRenderTarget(GameplayBuffers.Gameplay);

        GameplayRenderer.Begin();

        float alpha = MiaoNetModule.Settings.PlayerOpacityValue;
        Draw.SpriteBatch.Draw(GameplayBuffers.TempA, level.Camera.Position, Color.White * alpha);
    }
}
