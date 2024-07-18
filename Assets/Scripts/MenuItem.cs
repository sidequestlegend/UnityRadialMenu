using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Events;

[Serializable]
public class MenuItem{
    public string label;
    public Texture2D icon;
    public UnityEvent click;
    public MenuItem[] children;
    public MenuItem parent;
}