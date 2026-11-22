using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public sealed class PaymentIcon : Graphic
{
    public enum Symbol { Card, Lock, Shield, Check }
    public Symbol Kind { get; set; }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        switch (Kind)
        {
            case Symbol.Card:
                Line(mesh, .1f, .22f, .9f, .22f);
                Line(mesh, .9f, .22f, .9f, .78f);
                Line(mesh, .9f, .78f, .1f, .78f);
                Line(mesh, .1f, .78f, .1f, .22f);
                Line(mesh, .1f, .6f, .9f, .6f);
                break;
            case Symbol.Lock:
                Line(mesh, .24f, .08f, .76f, .08f);
                Line(mesh, .76f, .08f, .76f, .56f);
                Line(mesh, .76f, .56f, .24f, .56f);
                Line(mesh, .24f, .56f, .24f, .08f);
                Arc(mesh, .5f, .57f, .2f, 0, 180);
                Line(mesh, .3f, .57f, .3f, .7f);
                Line(mesh, .7f, .57f, .7f, .7f);
                Line(mesh, .5f, .25f, .5f, .39f);
                break;
            case Symbol.Shield:
                Line(mesh, .18f, .78f, .5f, .94f);
                Line(mesh, .5f, .94f, .82f, .78f);
                Line(mesh, .82f, .78f, .76f, .32f);
                Line(mesh, .76f, .32f, .5f, .07f);
                Line(mesh, .5f, .07f, .24f, .32f);
                Line(mesh, .24f, .32f, .18f, .78f);
                Tick(mesh);
                break;
            case Symbol.Check:
                Arc(mesh, .5f, .5f, .4f, 0, 360);
                Tick(mesh);
                break;
        }
    }

    private void Tick(VertexHelper mesh)
    {
        Line(mesh, .32f, .52f, .46f, .38f);
        Line(mesh, .46f, .38f, .7f, .65f);
    }

    private void Arc(VertexHelper mesh, float x, float y, float radius, float start, float end)
    {
        const int segments = 24;
        for (int i = 0; i < segments; i++)
        {
            float a = Mathf.Lerp(start, end, i / (float)segments) * Mathf.Deg2Rad;
            float b = Mathf.Lerp(start, end, (i + 1) / (float)segments) * Mathf.Deg2Rad;
            Line(mesh, x + Mathf.Cos(a) * radius, y + Mathf.Sin(a) * radius,
                x + Mathf.Cos(b) * radius, y + Mathf.Sin(b) * radius);
        }
    }

    private void Line(VertexHelper mesh, float x1, float y1, float x2, float y2)
    {
        var rect = rectTransform.rect;
        var a = new Vector2(rect.xMin + x1 * rect.width, rect.yMin + y1 * rect.height);
        var b = new Vector2(rect.xMin + x2 * rect.width, rect.yMin + y2 * rect.height);
        UIStroke.Line(mesh,a,b,color);
    }
}
