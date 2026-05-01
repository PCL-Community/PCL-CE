using System.Text.Json.Serialization;

namespace PCL.CE.Core.Minecraft.Java.UserPreference;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(ExistingJava), "exist")]
[JsonDerivedType(typeof(UseGlobalPreference), "global")]
[JsonDerivedType(typeof(UseRelativePath), "relative")]
[JsonDerivedType(typeof(AutoSelect), "auto")]
public abstract record JavaPreference;