return UnityEngine.Application.isPlaying && UnityEngine.Object.FindAnyObjectByType<Climbing.ThirdPersonController>() != null && UnityEngine.Time.timeSinceLevelLoad > 1.5f ? "PLAYING" : "NOT";
