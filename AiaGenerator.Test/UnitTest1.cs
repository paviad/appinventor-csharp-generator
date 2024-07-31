namespace AiaGenerator.Test;

public class UnitTest1 {
    [Fact]
    public void Test1() {
        var g = new Generator();
        g.Read("HelloPurr.aia");

        bool Testa() => false;
        while (Testa()) { }
    }
}

public class GeneratedGarbage {
    private void Click(string Location) {
        Invoke("Click", Location);
    }

    private bool Detect(string Template) {
        return Invoke_Bool("Detect", Template);
    }

    private void Invoke(string Name, params object[] Arguments) { }

    private bool Invoke_Bool(string Name, params object[] Arguments) {
        return true;
    }

    private bool MAIN() {
        bool LocalFunction2() {
            var Success = false;
            var Failure = false;
            decimal Count = 1;

            bool LocalFunction1() {
                while (!Success && !Failure && Count != 3) {
                    if (Detect("Select All") && Detect("Unload")) {
                        Click("Select All");
                        Click("Unload");
                        Success = true;
                    }
                    else {
                        if (Detect("Taskbar Icon")) {
                            Click("Taskbar Icon");
                        }
                        else {
                            Failure = true;
                        }
                    }

                    Count = Count + 1;
                }

                return Success;
            }

            return LocalFunction1();
        }

        return LocalFunction2();
    }

    private void Move(object x, object y) {
        Invoke("Move", x, y);
    }

    private bool Testa() {
        bool LocalFunction5() {
            Click("Taskbar Icon");
            return true;
        }

        bool LocalFunction4() {
            Click("Taskbar Icon");
            return true;
        }

        bool LocalFunction3() {
            Click("Taskbar Icon");
            return true;
        }

        return LocalFunction3() ? LocalFunction4() : LocalFunction5();
    }
}
