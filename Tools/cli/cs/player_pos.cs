var p = UnityEngine.Object.FindAnyObjectByType<Climbing.ThirdPersonController>();
return p == null ? "sem jogador" : p.transform.position + " rot " + p.transform.eulerAngles;
