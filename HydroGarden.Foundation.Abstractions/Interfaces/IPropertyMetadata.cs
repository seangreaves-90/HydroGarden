namespace HydroGarden.Foundation.Abstractions.Interfaces
{
    public interface IPropertyMetadata
    {
        public bool IsEditable { get; set; }
        public bool IsVisible { get; set; }
        public string? DisplayName { get; set; }
        public string? Description { get; set; }
        //public string? Category { get; set; }
        //public IPropertyMetadata? DisplayAfter{ get; init; }
        //public IPropertyMetadata? DisplayBefore { get; init; }
    }
}
