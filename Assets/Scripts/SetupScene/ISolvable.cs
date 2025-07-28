using System;
public interface ISolvable
{
    bool IsPuzzleSolved();
    System.Action<ISolvable> OnSolved { get; set; }
    void CheckSolution();

}
