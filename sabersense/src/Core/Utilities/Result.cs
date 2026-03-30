// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using System;

namespace SaberSense.Core.Utilities;

internal readonly struct Result<T>
{
    public readonly T Value;
    public readonly string? Error;
    public readonly bool IsSuccess;
    public bool IsFailure => !IsSuccess;

    private Result(T value, string? error, bool isSuccess)
    {
        Value = value;
        Error = error;
        IsSuccess = isSuccess;
    }

    public static Result<T> Success(T value) => new(value, null, true);

    public static Result<T> Failure(string error) => new(default!, error, false);

    public Result<TOut> Map<TOut>(Func<T, TOut> transform) =>
        IsSuccess ? Result<TOut>.Success(transform(Value)) : Result<TOut>.Failure(Error!);

    public void Match(Action<T> onSuccess, Action<string> onFailure)
    {
        if (IsSuccess) onSuccess(Value);
        else onFailure(Error!);
    }

    public T ValueOr(T fallback) => IsSuccess ? Value : fallback;

    public static implicit operator Result<T>(T value) => Success(value);

    public override string ToString() => IsSuccess ? $"Success({Value})" : $"Failure({Error})";
}