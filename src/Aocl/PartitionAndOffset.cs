namespace Aocl
{
  readonly struct PartitionAndOffset
  {
    public PartitionAndOffset(int partition, int offset)
    {
      Partition = partition;
      Offset = offset;
    }

    public int Partition { get; }
    public int Offset { get; }

    public void Deconstruct(out int partition, out int offset)
    {
      partition = Partition;
      offset = Offset;
    }
  }
}
