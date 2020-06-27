#pragma once

class ReservedMethodNames
{
public:
    // Note: Equals and ReferenceEquals are not a problem here because they take managed parameters
    void GetHashCode();
    void GetType();
    void MemberwiseClone();
    void ToString();
};

class ReservedMethodNames2
{
public:
    int Equals;
    int GetHashCode;
    int GetType;
    int MemberwiseClone;
    int ReferenceEquals;
    int ToString;
};

