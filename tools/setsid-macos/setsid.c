#include <stdlib.h>
#include <unistd.h>
#include <err.h>

int main(int argc, char* argv[]) {
    if (argc < 2) {
        err(EXIT_FAILURE, "Usage: setsid <program> [arguments ...]");
    }

    if (setsid() < 0) {
        err(EXIT_FAILURE, "setsid failed");
    }

    execvp(argv[1], argv + 1);
    err(EXIT_FAILURE, "Failed to execute %s", argv[1]);
}
