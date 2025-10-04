// Copyright (c) Le Loc Tai <leloctai.com> . All rights reserved. Do not redistribute.

using System;
using System.Collections.Generic;
using LeTai.Asset.TranslucentImage;
using LeTai.Common;
using LeTai.Paraform.Scaffold;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace LeTai.Paraform.Demo
{
public class DemoControllerPresets : MonoBehaviour
{
    [Serializable]
    public struct ShapePreset
    {
        public float cornerCurvature;
        public float filletCurvature;
        public float radius;

        public void Apply(TranslucentImage target)
        {
            target.paraformConfig.CornerCurvature = cornerCurvature;
            target.paraformConfig.FilletCurvature = filletCurvature;
            target.paraformConfig.CornerRadii     = new Vector4(radius, radius, radius, radius);
        }

        public void ApplyAnimated(TranslucentImage target, TinyTween.Spring spring)
        {
            TinyTween.Animate(target, target.paraformConfig.CornerCurvature, cornerCurvature,
                              static (ti, next) => ti.paraformConfig.CornerCurvature = next, spring);
            TinyTween.Animate(target, target.paraformConfig.FilletCurvature, filletCurvature,
                              static (ti, next) => ti.paraformConfig.FilletCurvature = next, spring);
            TinyTween.Animate(target, target.paraformConfig.CornerRadii.x, radius,
                              static (ti, next) => ti.paraformConfig.CornerRadii = new Vector4(next, next, next, next), spring);
        }
    }

    [Serializable]
    public struct MaterialPreset
    {
        public float blurStrength;
        public float dispersion;
    }

    public TranslucentImage     target;
    public GameObject           shapePresetGroup;
    public TinyTween.Spring     shapeSpring  = TinyTween.Spring.DEFAULT;
    public List<ShapePreset>    shapePresets = new();
    public GameObject           blurPresetGroup;
    public TinyTween.Spring     materialSpring  = TinyTween.Spring.DEFAULT;
    public List<MaterialPreset> materialPresets = new();


    internal ShapePreset defaultShapePreset;

    TranslucentImageSource _source;
    ScalableBlurConfig     _blurConfig;
    Material               _mat;
    int                    _activeMaterialPresetIndex;
    bool                   _isChromaticEnabled = true;

    void Awake()
    {
        _blurConfig              = (ScalableBlurConfig)Instantiate(target.source.BlurConfig);
        target.source.BlurConfig = _blurConfig;

        _mat            = Instantiate(target.material);
        target.material = _mat;

        int defaultPresetIndex = 0;

        var toggles = shapePresetGroup.GetComponentsInChildren<Toggle>();
        for (var i = 0; i < toggles.Length; i++)
        {
            var presetIndex = i;
            toggles[i].onValueChanged.AddListener(_ => SetShapePreset(shapePresets[presetIndex]));
            if (toggles[i].isOn)
                defaultPresetIndex = i;
        }

        defaultShapePreset = shapePresets[defaultPresetIndex];
        defaultShapePreset.Apply(target);

        toggles = blurPresetGroup.GetComponentsInChildren<Toggle>();
        for (var i = 0; i < toggles.Length; i++)
        {
            var presetIndex = i;
            toggles[i].onValueChanged.AddListener(_ => SetMaterialPresetIndex(presetIndex));
            if (toggles[i].isOn)
                defaultPresetIndex = i;
        }
        _activeMaterialPresetIndex = defaultPresetIndex;
        ApplyActiveMaterialPreset();
    }

    public void SetShapePreset(ShapePreset preset)
    {
        ShapePreset effectivePreset = new ShapePreset {
            cornerCurvature = preset.cornerCurvature >= 0 ? preset.cornerCurvature : defaultShapePreset.cornerCurvature,
            filletCurvature = preset.filletCurvature >= 0 ? preset.filletCurvature : defaultShapePreset.filletCurvature,
            radius          = preset.radius >= 0 ? preset.radius : defaultShapePreset.radius
        };

        effectivePreset.ApplyAnimated(target, shapeSpring);
    }

    public void ApplyActiveMaterialPreset()
    {
        SetMaterialPresetIndex(_activeMaterialPresetIndex);
    }

    public void SetMaterialPresetIndex(int index)
    {
        _activeMaterialPresetIndex = index;
        var preset = materialPresets[index];
        SetBlur(preset.blurStrength);
        SetDispersion(preset.dispersion);
    }

    public void SetBlur(float blur)
    {
        TinyTween.Animate(_blurConfig, _blurConfig.Strength, blur,
                          static (cfg, next) => cfg.Strength = next,
                          materialSpring);
    }

    void SetDispersion(float dispersion)
    {
        if (!_isChromaticEnabled)
            dispersion = 0;

        TinyTween.Animate(_mat, _mat.GetFloat(ShaderID.CHROMATIC_DISPERSION_DUMMY), dispersion,
                          static (mat, next) => ParaformMaterial.SetDispersion(mat, next),
                          materialSpring);
    }

    public void SetChromaticEnabled(bool isEnabled)
    {
        _isChromaticEnabled = isEnabled;
        SetDispersion(materialPresets[_activeMaterialPresetIndex].dispersion);
    }
}
}
