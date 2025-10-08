using System.Collections.Generic;
using UnityEngine;

public enum TeamId { Blue, Red }

public static class TeamRegistry
{
    private static readonly List<PlayerController> all = new();

    public static void Register(PlayerController p)
    {
        if (p != null && !all.Contains(p)) all.Add(p);
    }

    public static void Unregister(PlayerController p)
    {
        if (p != null) all.Remove(p);
    }

    public static PlayerController GetClosestTeammate(PlayerController from)
    {
        if (from == null) return null;
        PlayerController best = null;
        float bestDist = float.MaxValue;

        foreach (var p in all)
        {
            if (!p || p == from || p.teamId != from.teamId) continue;
            float d = (p.transform.position - from.transform.position).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = p; }
        }
        return best;
    }

    public static PlayerController GetClosestTeammateToBall(TeamId team)
    {
        var ball = BallController.Instance;
        if (!ball) return null;

        PlayerController best = null;
        float bestDist = float.MaxValue;

        foreach (var p in all)
        {
            if (!p || p.teamId != team) continue;
            float d = (p.transform.position - ball.transform.position).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = p; }
        }
        return best;
    }

    /// <summary>
    /// Compañero más cercano dentro de un sector CARDINAL estricto.
    /// dirCardinal debe ser exactamente (1,0),(-1,0),(0,1) o (0,-1).
    /// maxAngleDeg: tolerancia angular alrededor del eje (ej. 20°).
    /// minForward: avance mínimo en el eje (para ignorar el “atrás” cuando pides “adelante”).
    /// </summary>
    public static PlayerController GetClosestTeammateInCardinal(
        PlayerController from,
        Vector2 dirCardinal,
        float maxAngleDeg = 20f,
        float minForward = 0.05f)
    {
        if (!from) return null;
        if (dirCardinal == Vector2.zero) return null;
        dirCardinal = new Vector2(Mathf.Sign(dirCardinal.x), Mathf.Sign(dirCardinal.y));

        // Ejes y su perpendicular
        Vector2 forwardAxis = dirCardinal; // unitario en cardenal
        Vector2 rightAxis = new Vector2(-forwardAxis.y, forwardAxis.x); // perpendicular

        float maxTan = Mathf.Tan(maxAngleDeg * Mathf.Deg2Rad); // tolerancia lateral proporcional

        PlayerController best = null;
        float bestForward = float.MaxValue; // priorizamos el más cercano en el eje

        foreach (var p in all)
        {
            if (!p || p == from || p.teamId != from.teamId) continue;

            Vector2 delta = (Vector2)(p.transform.position - from.transform.position);
            float fwd = Vector2.Dot(delta, forwardAxis); // proyección en el eje (puede ser negativa)
            if (fwd <= minForward) continue;             // debe estar hacia “adelante” en ese eje

            float lat = Mathf.Abs(Vector2.Dot(delta, rightAxis)); // desviación lateral
            // Limitar por ángulo: |lat|/fwd <= tan(maxAngle)
            if (fwd <= 0f) continue;
            float ratio = lat / fwd;
            if (ratio > maxTan) continue;

            // Elegimos el menor avance “fwd” (más cercano en esa dirección)
            if (fwd < bestForward)
            {
                bestForward = fwd;
                best = p;
            }
        }
        return best;
    }
}
