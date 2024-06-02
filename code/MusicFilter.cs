using System;
using System.Collections.Generic;
using Sandbox;
using Sandbox.Audio;

namespace Instagib;

public class MusicFilter : AudioProcessor
{
    [Property] float Amount { get; set; } = 0f;

    [Hide] private PerChannel<float> previousInput = 0.0f;
    [Hide] private PerChannel<float> previousOutput = 0.0f;

    protected override void ProcessSingleChannel( AudioChannel c, Span<float> input )
    {
        var cc = c.Get();
        float alpha = (float)Math.Pow(Amount, 2).Clamp(0, 1);

        for (int i = 0; i < input.Length; i++)
        {
            float currentInput = input[i];
            input[i] = alpha * (previousOutput.Value[cc] + currentInput - previousInput.Value[cc]);
            previousInput.Value[cc] = currentInput;
            previousOutput.Value[cc] = input[i];
        }
    }
}