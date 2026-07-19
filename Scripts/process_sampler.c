#include <errno.h>
#include <libproc.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <sys/resource.h>
#include <time.h>

static double monotonic_seconds(void) {
    struct timespec value;
    if (clock_gettime(CLOCK_MONOTONIC_RAW, &value) != 0) {
        perror("clock_gettime");
        exit(EXIT_FAILURE);
    }
    return (double)value.tv_sec + (double)value.tv_nsec / 1000000000.0;
}

static int copy_usage(pid_t pid, struct rusage_info_v4 *usage) {
    memset(usage, 0, sizeof(*usage));
    return proc_pid_rusage(pid, RUSAGE_INFO_V4, (rusage_info_t *)usage);
}

static int integer_argument(
    int argc,
    char *const argv[],
    const char *name,
    int fallback
) {
    for (int index = 1; index + 1 < argc; ++index) {
        if (strcmp(argv[index], name) == 0) {
            char *end = NULL;
            long value = strtol(argv[index + 1], &end, 10);
            if (end != argv[index + 1] && *end == '\0' && value > 0 && value <= INT32_MAX) {
                return (int)value;
            }
            return fallback;
        }
    }
    return fallback;
}

int main(int argc, char *const argv[]) {
    const pid_t pid = (pid_t)integer_argument(argc, argv, "--pid", 0);
    const int seconds = integer_argument(argc, argv, "--seconds", 60);
    const int interval_ms = integer_argument(argc, argv, "--interval-ms", 1000);
    if (pid <= 0 || seconds <= 0 || interval_ms <= 0) {
        fputs("usage: process_sampler --pid PID --seconds N [--interval-ms N]\n", stderr);
        return EXIT_FAILURE;
    }

    struct rusage_info_v4 first;
    if (copy_usage(pid, &first) != 0) {
        fprintf(stderr, "process_sampler: cannot sample pid %d\n", pid);
        return EXIT_FAILURE;
    }

    const double started = monotonic_seconds();
    const double deadline = started + (double)seconds;
    uint64_t resident_sum = first.ri_resident_size;
    uint64_t resident_max = first.ri_resident_size;
    uint64_t footprint_sum = first.ri_phys_footprint;
    uint64_t footprint_max = first.ri_phys_footprint;
    uint64_t samples = 1;
    struct rusage_info_v4 current = first;

    while (monotonic_seconds() < deadline) {
        struct timespec delay = {
            .tv_sec = interval_ms / 1000,
            .tv_nsec = (long)(interval_ms % 1000) * 1000000L,
        };
        while (nanosleep(&delay, &delay) != 0 && errno == EINTR) {
        }
        if (copy_usage(pid, &current) != 0) {
            fprintf(stderr, "process_sampler: pid %d exited before the sample completed\n", pid);
            return EXIT_FAILURE;
        }
        resident_sum += current.ri_resident_size;
        if (current.ri_resident_size > resident_max) {
            resident_max = current.ri_resident_size;
        }
        footprint_sum += current.ri_phys_footprint;
        if (current.ri_phys_footprint > footprint_max) {
            footprint_max = current.ri_phys_footprint;
        }
        ++samples;
    }

    const double elapsed = monotonic_seconds() - started;
    const uint64_t first_cpu = first.ri_user_time + first.ri_system_time;
    const uint64_t current_cpu = current.ri_user_time + current.ri_system_time;
    const uint64_t cpu_delta = current_cpu >= first_cpu ? current_cpu - first_cpu : 0;
    const uint64_t idle_wakeups = current.ri_pkg_idle_wkups >= first.ri_pkg_idle_wkups
        ? current.ri_pkg_idle_wkups - first.ri_pkg_idle_wkups
        : 0;
    const uint64_t interrupt_wakeups = current.ri_interrupt_wkups >= first.ri_interrupt_wkups
        ? current.ri_interrupt_wkups - first.ri_interrupt_wkups
        : 0;
    const double megabyte = 1024.0 * 1024.0;

    printf(
        "{\"pid\":%d,\"samples\":%llu,\"duration_seconds\":%.3f,"
        "\"average_cpu_percent\":%.4f,\"average_resident_mb\":%.3f,"
        "\"max_resident_mb\":%.3f,\"average_raw_rss_mb\":%.3f,"
        "\"max_raw_rss_mb\":%.3f,\"wakeups_per_second\":%.4f,"
        "\"interrupt_wakeups_per_second\":%.4f}\n",
        pid,
        (unsigned long long)samples,
        elapsed,
        elapsed > 0.0 ? (double)cpu_delta / 1000000000.0 / elapsed * 100.0 : 0.0,
        (double)footprint_sum / (double)samples / megabyte,
        (double)footprint_max / megabyte,
        (double)resident_sum / (double)samples / megabyte,
        (double)resident_max / megabyte,
        elapsed > 0.0 ? (double)idle_wakeups / elapsed : 0.0,
        elapsed > 0.0 ? (double)interrupt_wakeups / elapsed : 0.0
    );
    return EXIT_SUCCESS;
}
