namespace Assets.Scripts.Core_logic
{
    public class Player
    {
        public int Id { get; private set; }
        public int Score { get; set; }
        public int MeeplesAvailable { get; set; }

        public bool HasAbbot { get; set; }

        public Player(int id)
        {
            Id = id;
            Score = 0;
            MeeplesAvailable = 7; // Стандартное число в Каркассоне
            HasAbbot = true;
        }

        public Player Clone()
        {
            return new Player(this.Id)
            {
                Score = this.Score,
                MeeplesAvailable = this.MeeplesAvailable, 
                HasAbbot = this.HasAbbot
            };
        }
    }
}