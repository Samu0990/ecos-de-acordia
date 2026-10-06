namespace Aren.World
{
    /// <summary>
    /// Pontuação "Notas" (contador do canto superior direito do HUD, como na referência do autor):
    /// cada Eco dissipado vale 100 + 10 por golpe da cadência atual, chefe 1000, contra-ataque 50,
    /// esquiva perfeita 25. Zera quando um jogo novo começa.
    /// </summary>
    public static class Notas
    {
        public static int Value { get; private set; }
        public static event System.Action<int, int> OnChanged;   // (total, quanto entrou)

        public static void Reset() { Value = 0; OnChanged?.Invoke(0, 0); }

        public static void Add(int amount)
        {
            if (amount <= 0) return;
            Value += amount;
            OnChanged?.Invoke(Value, amount);
        }
    }
}
