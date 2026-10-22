// src/PropTraderTools/Features/PttGlobalBreakEven.cs
// B180 DW-B180-BE-SWAP-ALL: thin buffer holder only.
// Execute(int) removed -- use PttBeSwapAll.Execute(bufferTicks) instead.
// Keeps GlobalBeBuffer, IncrementBuffer, DecrementBuffer for panel +/- spin UI.
// CYC=2 (IncrementBuffer), CYC=2 (DecrementBuffer).
// JS-021: no lock (volatile int). JS-001: no throw. ASCII-only.

namespace PropTraderTools
{
    internal sealed class PttGlobalBreakEven
    {
        private volatile int _globalBeBuffer = 0;

        internal int GlobalBeBuffer => _globalBeBuffer;

        internal void IncrementBuffer()
        {
            if (_globalBeBuffer < 50) // B214 Lane B: raised from 10 to 50
                _globalBeBuffer++;
            CopyEngine.Instance.RaiseBeBufferChanged(_globalBeBuffer);
        }

        internal void DecrementBuffer()
        {
            if (_globalBeBuffer > -50) // B214 Lane B: raised from -10 to -50
                _globalBeBuffer--;
            CopyEngine.Instance.RaiseBeBufferChanged(_globalBeBuffer);
        }
    }
}
