namespace Ptt;

public record Symbol(String Name);

public record Term;

public record Atom(Symbol Symbol) : Term;

public class CompositionConfig
{
}

public record Composition(CompositionConfig Config, Term[] Children, Symbol? Symbol = null);

