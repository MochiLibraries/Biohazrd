#pragma once

class ConstOverloads
{
public:
    ConstOverloads& Method();
    ConstOverloads& Method() const;
};
