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
}