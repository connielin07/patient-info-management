namespace WebApplication1.Models
{
    public class Cat
    {
        public string Name { get; set; }
        public string Color { get; set; }
        public double Weight { get; set; }
        public void Yell() {
            Console.WriteLine($"{Name}:meow");
        }
    }
}
