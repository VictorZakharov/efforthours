<x-layout>
  @if($enabled)
    <form method="get">
      <label>Query <input name="query"></label>
      <button type="submit">Search</button>
    </form>
  @endif
</x-layout>
