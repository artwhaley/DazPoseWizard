using System;
using UnityEngine;
namespace DazPose.Performer {
 public enum PerformerMaterialMode { Regular, GlowyWireframe }
 [CreateAssetMenu(menuName="DazPose/Performer/Magic Shader Profile")]
 public sealed class PerformerMagicShaderProfile : ScriptableObject {
  public PerformerMaterialMode mode;
  public Material wireframeMaterial;
  public void ValidateConfiguration() {
   if(mode==PerformerMaterialMode.GlowyWireframe && (wireframeMaterial==null || wireframeMaterial.shader==null))
    throw new InvalidOperationException("GlowyWireframe requires the project-owned glowing wireframe material.");
  }
 }
}