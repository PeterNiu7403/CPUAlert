#include "DarwinBridge.h"

#include <CoreFoundation/CoreFoundation.h>
#include <IOKit/IOKitLib.h>
#include <dlfcn.h>
#include <libproc.h>
#include <mach/mach.h>
#include <mach/mach_host.h>
#include <math.h>
#include <pthread.h>
#include <stdlib.h>
#include <string.h>
#include <sys/proc_info.h>
#include <sys/resource.h>

bool CPUACopySystemTicks(CPUASystemTicks *output) {
    if (output == NULL) return false;
    host_cpu_load_info_data_t info = {0};
    mach_msg_type_number_t count = HOST_CPU_LOAD_INFO_COUNT;
    kern_return_t result = host_statistics(
        mach_host_self(), HOST_CPU_LOAD_INFO,
        (host_info_t)&info, &count
    );
    if (result != KERN_SUCCESS) return false;
    output->user = info.cpu_ticks[CPU_STATE_USER];
    output->system = info.cpu_ticks[CPU_STATE_SYSTEM];
    output->idle = info.cpu_ticks[CPU_STATE_IDLE];
    output->nice = info.cpu_ticks[CPU_STATE_NICE];
    return true;
}

int CPUACopyAllPIDs(pid_t *buffer, int buffer_bytes) {
    return proc_listpids(PROC_ALL_PIDS, 0, buffer, buffer_bytes);
}

bool CPUACopyProcessCounter(pid_t pid, CPUAProcessCounter *output) {
    if (output == NULL || pid <= 0) return false;
    struct proc_bsdinfo bsd = {0};
    if (proc_pidinfo(pid, PROC_PIDTBSDINFO, 0, &bsd, sizeof(bsd)) != sizeof(bsd)) {
        return false;
    }
    rusage_info_current usage = {0};
    if (proc_pid_rusage(pid, RUSAGE_INFO_CURRENT, (rusage_info_t *)&usage) != 0) {
        return false;
    }
    memset(output, 0, sizeof(*output));
    output->pid = pid;
    output->start_time_ns = (uint64_t)bsd.pbi_start_tvsec * 1000000000ULL
        + (uint64_t)bsd.pbi_start_tvusec * 1000ULL;
    output->cpu_time_ns = usage.ri_user_time + usage.ri_system_time;
    output->uid = bsd.pbi_uid;
    strlcpy(output->name, bsd.pbi_name, sizeof(output->name));
    return true;
}

int CPUACopyThreadIDs(pid_t pid, uint64_t *buffer, int buffer_bytes) {
    return proc_pidinfo(pid, PROC_PIDLISTTHREADS, 0, buffer, buffer_bytes);
}

bool CPUACopyThreadCounter(pid_t pid, uint64_t thread_id, CPUAThreadCounter *output) {
    if (output == NULL || pid <= 0 || thread_id == 0) return false;
    struct proc_threadinfo info = {0};
    int size = proc_pidinfo(pid, PROC_PIDTHREADINFO, thread_id, &info, sizeof(info));
    if (size != sizeof(info)) return false;
    memset(output, 0, sizeof(*output));
    output->thread_id = thread_id;
    output->cpu_time_ns = info.pth_user_time + info.pth_system_time;
    strlcpy(output->name, info.pth_name, sizeof(output->name));
    return true;
}

typedef CFDictionaryRef (*CPUAIOReportCopyChannelsInGroupFn)(
    CFStringRef, CFStringRef, uint64_t, uint64_t, uint64_t
);
typedef CFTypeRef (*CPUAIOReportCreateSubscriptionFn)(
    void *, CFMutableDictionaryRef, CFMutableDictionaryRef *, uint64_t, CFTypeRef
);
typedef CFDictionaryRef (*CPUAIOReportCreateSamplesFn)(
    CFTypeRef, CFMutableDictionaryRef, CFTypeRef
);
typedef CFDictionaryRef (*CPUAIOReportCreateSamplesDeltaFn)(
    CFDictionaryRef, CFDictionaryRef, CFTypeRef
);
typedef CFStringRef (*CPUAIOReportChannelStringFn)(CFDictionaryRef);
typedef int (*CPUAIOReportStateGetCountFn)(CFDictionaryRef);
typedef CFStringRef (*CPUAIOReportStateGetNameForIndexFn)(CFDictionaryRef, int);
typedef int64_t (*CPUAIOReportStateGetResidencyFn)(CFDictionaryRef, int);

typedef struct {
    CPUAIOReportCopyChannelsInGroupFn copy_channels;
    CPUAIOReportCreateSubscriptionFn create_subscription;
    CPUAIOReportCreateSamplesFn create_samples;
    CPUAIOReportCreateSamplesDeltaFn create_delta;
    CPUAIOReportChannelStringFn channel_name;
    CPUAIOReportChannelStringFn channel_group;
    CPUAIOReportStateGetCountFn state_count;
    CPUAIOReportStateGetNameForIndexFn state_name;
    CPUAIOReportStateGetResidencyFn state_residency;
} CPUAIOReportFunctions;

typedef struct {
    void *iokit_handle;
    CPUAIOReportFunctions io_report;
    CFTypeRef subscription;
    CFMutableDictionaryRef subscribed_channels;
    CFDictionaryRef previous_sample;
    bool io_report_ready;
} CPUAGPUContext;

static bool CPUAStringContainsCaseInsensitive(CFStringRef value, CFStringRef needle) {
    if (value == NULL || needle == NULL) return false;
    CFRange found = CFStringFind(value, needle, kCFCompareCaseInsensitive);
    return found.location != kCFNotFound;
}

static void *CPUASymbol(void *handle, const char *name) {
    if (handle == NULL || name == NULL) return NULL;
    dlerror();
    void *symbol = dlsym(handle, name);
    return dlerror() == NULL ? symbol : NULL;
}

static CFMutableDictionaryRef CPUACopyCombinedGPUChannels(
    CPUAIOReportCopyChannelsInGroupFn copy_channels
) {
    if (copy_channels == NULL) return NULL;
    CFDictionaryRef stats = copy_channels(CFSTR("GPU Stats"), NULL, 0, 0, 0);
    CFDictionaryRef states = copy_channels(
        CFSTR("GPU Performance States"), NULL, 0, 0, 0
    );
    CFMutableDictionaryRef combined = CFDictionaryCreateMutable(
        kCFAllocatorDefault,
        1,
        &kCFTypeDictionaryKeyCallBacks,
        &kCFTypeDictionaryValueCallBacks
    );
    if (combined == NULL) {
        if (stats != NULL) CFRelease(stats);
        if (states != NULL) CFRelease(states);
        return NULL;
    }

    CFMutableArrayRef channels = CFArrayCreateMutable(
        kCFAllocatorDefault, 0, &kCFTypeArrayCallBacks
    );
    if (channels == NULL) {
        CFRelease(combined);
        if (stats != NULL) CFRelease(stats);
        if (states != NULL) CFRelease(states);
        return NULL;
    }

    const void *key = CFSTR("IOReportChannels");
    CFDictionaryRef dictionaries[2] = {stats, states};
    for (size_t index = 0; index < 2; index++) {
        CFDictionaryRef dictionary = dictionaries[index];
        if (dictionary == NULL || CFGetTypeID(dictionary) != CFDictionaryGetTypeID()) {
            continue;
        }
        CFTypeRef value = CFDictionaryGetValue(dictionary, key);
        if (value == NULL || CFGetTypeID(value) != CFArrayGetTypeID()) continue;
        CFArrayRef array = (CFArrayRef)value;
        CFIndex count = CFArrayGetCount(array);
        for (CFIndex item = 0; item < count; item++) {
            CFTypeRef channel = CFArrayGetValueAtIndex(array, item);
            if (channel != NULL) CFArrayAppendValue(channels, channel);
        }
    }

    if (CFArrayGetCount(channels) > 0) {
        CFDictionarySetValue(combined, key, channels);
    }
    CFRelease(channels);
    if (stats != NULL) CFRelease(stats);
    if (states != NULL) CFRelease(states);

    if (CFDictionaryGetCount(combined) == 0) {
        CFRelease(combined);
        return NULL;
    }
    return combined;
}

static bool CPUALoadIOReport(CPUAGPUContext *context) {
    if (context == NULL) return false;
    context->iokit_handle = dlopen(
        "/System/Library/Frameworks/IOKit.framework/Versions/A/IOKit",
        RTLD_LAZY | RTLD_LOCAL
    );
    if (context->iokit_handle == NULL) return false;

#define CPUA_LOAD_IOREPORT(field, type, symbol_name) \
    context->io_report.field = (type)CPUASymbol(context->iokit_handle, symbol_name); \
    if (context->io_report.field == NULL) return false

    CPUA_LOAD_IOREPORT(
        copy_channels,
        CPUAIOReportCopyChannelsInGroupFn,
        "IOReportCopyChannelsInGroup"
    );
    CPUA_LOAD_IOREPORT(
        create_subscription,
        CPUAIOReportCreateSubscriptionFn,
        "IOReportCreateSubscription"
    );
    CPUA_LOAD_IOREPORT(
        create_samples,
        CPUAIOReportCreateSamplesFn,
        "IOReportCreateSamples"
    );
    CPUA_LOAD_IOREPORT(
        create_delta,
        CPUAIOReportCreateSamplesDeltaFn,
        "IOReportCreateSamplesDelta"
    );
    CPUA_LOAD_IOREPORT(
        channel_name,
        CPUAIOReportChannelStringFn,
        "IOReportChannelGetChannelName"
    );
    CPUA_LOAD_IOREPORT(
        channel_group,
        CPUAIOReportChannelStringFn,
        "IOReportChannelGetGroup"
    );
    CPUA_LOAD_IOREPORT(
        state_count,
        CPUAIOReportStateGetCountFn,
        "IOReportStateGetCount"
    );
    CPUA_LOAD_IOREPORT(
        state_name,
        CPUAIOReportStateGetNameForIndexFn,
        "IOReportStateGetNameForIndex"
    );
    CPUA_LOAD_IOREPORT(
        state_residency,
        CPUAIOReportStateGetResidencyFn,
        "IOReportStateGetResidency"
    );
#undef CPUA_LOAD_IOREPORT

    CFMutableDictionaryRef channels = CPUACopyCombinedGPUChannels(
        context->io_report.copy_channels
    );
    if (channels == NULL) return false;
    context->subscription = context->io_report.create_subscription(
        NULL,
        channels,
        &context->subscribed_channels,
        0,
        NULL
    );
    CFRelease(channels);
    return context->subscription != NULL && context->subscribed_channels != NULL;
}

static bool CPUACopyIOReportUsage(CPUAGPUContext *context, double *usage) {
    if (context == NULL || usage == NULL || !context->io_report_ready) return false;
    CFDictionaryRef current = context->io_report.create_samples(
        context->subscription,
        context->subscribed_channels,
        NULL
    );
    if (current == NULL) return false;
    if (context->previous_sample == NULL) {
        context->previous_sample = current;
        return false;
    }

    CFDictionaryRef delta = context->io_report.create_delta(
        context->previous_sample,
        current,
        NULL
    );
    CFRelease(context->previous_sample);
    context->previous_sample = current;
    if (delta == NULL || CFGetTypeID(delta) != CFDictionaryGetTypeID()) {
        if (delta != NULL) CFRelease(delta);
        return false;
    }

    CFTypeRef raw_channels = CFDictionaryGetValue(delta, CFSTR("IOReportChannels"));
    if (raw_channels == NULL || CFGetTypeID(raw_channels) != CFArrayGetTypeID()) {
        CFRelease(delta);
        return false;
    }

    long double active = 0;
    long double total = 0;
    CFArrayRef channels = (CFArrayRef)raw_channels;
    CFIndex channel_count = CFArrayGetCount(channels);
    for (CFIndex channel_index = 0; channel_index < channel_count; channel_index++) {
        CFTypeRef raw_channel = CFArrayGetValueAtIndex(channels, channel_index);
        if (raw_channel == NULL || CFGetTypeID(raw_channel) != CFDictionaryGetTypeID()) {
            continue;
        }
        CFDictionaryRef channel = (CFDictionaryRef)raw_channel;
        CFStringRef group = context->io_report.channel_group(channel);
        CFStringRef name = context->io_report.channel_name(channel);
        if (!CPUAStringContainsCaseInsensitive(group, CFSTR("GPU")) &&
            !CPUAStringContainsCaseInsensitive(name, CFSTR("GPU"))) {
            continue;
        }

        int state_count = context->io_report.state_count(channel);
        if (state_count <= 0 || state_count > 1024) continue;
        for (int state_index = 0; state_index < state_count; state_index++) {
            int64_t residency = context->io_report.state_residency(channel, state_index);
            if (residency < 0) continue;
            total += (long double)residency;
            CFStringRef state_name = context->io_report.state_name(channel, state_index);
            if (CPUAStringContainsCaseInsensitive(state_name, CFSTR("active"))) {
                active += (long double)residency;
            }
        }
    }
    CFRelease(delta);

    if (total <= 0 || active < 0 || active > total) return false;
    double value = (double)(active / total);
    if (!isfinite(value) || value < 0 || value > 1) return false;
    *usage = value;
    return true;
}

static bool CPUACopyIOAcceleratorUsage(double *usage) {
    if (usage == NULL) return false;
    CFMutableDictionaryRef matching = IOServiceMatching("IOAccelerator");
    if (matching == NULL) return false;
    io_iterator_t iterator = IO_OBJECT_NULL;
    if (IOServiceGetMatchingServices(kIOMainPortDefault, matching, &iterator) != KERN_SUCCESS) {
        return false;
    }

    bool found = false;
    io_service_t service = IO_OBJECT_NULL;
    while (!found && (service = IOIteratorNext(iterator)) != IO_OBJECT_NULL) {
        CFTypeRef statistics = IORegistryEntryCreateCFProperty(
            service,
            CFSTR("PerformanceStatistics"),
            kCFAllocatorDefault,
            0
        );
        if (statistics != NULL && CFGetTypeID(statistics) == CFDictionaryGetTypeID()) {
            CFTypeRef raw_value = CFDictionaryGetValue(
                (CFDictionaryRef)statistics,
                CFSTR("Device Utilization %")
            );
            if (raw_value != NULL && CFGetTypeID(raw_value) == CFNumberGetTypeID()) {
                double percent = 0;
                if (CFNumberGetValue((CFNumberRef)raw_value, kCFNumberDoubleType, &percent) &&
                    isfinite(percent) && percent >= 0 && percent <= 100) {
                    *usage = percent / 100.0;
                    found = true;
                }
            }
        }
        if (statistics != NULL) CFRelease(statistics);
        IOObjectRelease(service);
    }
    IOObjectRelease(iterator);
    return found;
}

void *CPUACreateGPUContext(void) {
    CPUAGPUContext *context = calloc(1, sizeof(*context));
    if (context == NULL) return NULL;
    context->io_report_ready = CPUALoadIOReport(context);
    if (!context->io_report_ready) {
        if (context->subscription != NULL) {
            CFRelease(context->subscription);
            context->subscription = NULL;
        }
        if (context->subscribed_channels != NULL) {
            CFRelease(context->subscribed_channels);
            context->subscribed_channels = NULL;
        }
        memset(&context->io_report, 0, sizeof(context->io_report));
    }
    return context;
}

void CPUADestroyGPUContext(void *raw_context) {
    CPUAGPUContext *context = raw_context;
    if (context == NULL) return;
    if (context->previous_sample != NULL) CFRelease(context->previous_sample);
    if (context->subscribed_channels != NULL) CFRelease(context->subscribed_channels);
    if (context->subscription != NULL) CFRelease(context->subscription);
    if (context->iokit_handle != NULL) dlclose(context->iokit_handle);
    free(context);
}

bool CPUACopyGPUSample(void *raw_context, CPUAGPUSample *output) {
    CPUAGPUContext *context = raw_context;
    if (context == NULL || output == NULL) return false;
    output->usage = 0;
    output->source = CPUA_GPU_SOURCE_UNAVAILABLE;

    double usage = 0;
    if (CPUACopyIOReportUsage(context, &usage)) {
        output->usage = usage;
        output->source = CPUA_GPU_SOURCE_IOREPORT;
        return true;
    }
    if (CPUACopyIOAcceleratorUsage(&usage)) {
        output->usage = usage;
        output->source = CPUA_GPU_SOURCE_IOACCELERATOR;
        return true;
    }
    return true;
}

#define CPUA_PROC_PIDCOALITIONINFO 20
#define CPUA_COALITION_TYPE_RESOURCE 0
#define CPUA_COALITION_NUM_TYPES 2
#define CPUA_COALITION_NUM_THREAD_QOS_TYPES 7

typedef struct {
    uint64_t coalition_id[CPUA_COALITION_NUM_TYPES];
    uint64_t reserved1;
    uint64_t reserved2;
    uint64_t reserved3;
} CPUAProcPIDCoalitionInfo;

typedef struct {
    uint64_t tasks_started;
    uint64_t tasks_exited;
    uint64_t time_nonempty;
    uint64_t cpu_time;
    uint64_t interrupt_wakeups;
    uint64_t platform_idle_wakeups;
    uint64_t bytesread;
    uint64_t byteswritten;
    uint64_t gpu_time;
    uint64_t cpu_time_billed_to_me;
    uint64_t cpu_time_billed_to_others;
    uint64_t energy;
    uint64_t logical_immediate_writes;
    uint64_t logical_deferred_writes;
    uint64_t logical_invalidated_writes;
    uint64_t logical_metadata_writes;
    uint64_t logical_immediate_writes_to_external;
    uint64_t logical_deferred_writes_to_external;
    uint64_t logical_invalidated_writes_to_external;
    uint64_t logical_metadata_writes_to_external;
    uint64_t energy_billed_to_me;
    uint64_t energy_billed_to_others;
    uint64_t cpu_ptime;
    uint64_t cpu_time_eqos_len;
    uint64_t cpu_time_eqos[CPUA_COALITION_NUM_THREAD_QOS_TYPES];
    uint64_t cpu_instructions;
    uint64_t cpu_cycles;
    uint64_t fs_metadata_writes;
    uint64_t pm_writes;
    uint64_t cpu_pinstructions;
    uint64_t cpu_pcycles;
    uint64_t conclave_mem;
    uint64_t ane_mach_time;
    uint64_t ane_energy_nj;
    uint64_t phys_footprint;
    uint64_t gpu_energy_nj;
    uint64_t gpu_energy_nj_billed_to_me;
    uint64_t gpu_energy_nj_billed_to_others;
    uint64_t swapins;
} CPUACoalitionResourceUsage;

typedef int (*CPUACoalitionInfoResourceUsageFn)(
    uint64_t, CPUACoalitionResourceUsage *, size_t
);

static CPUACoalitionInfoResourceUsageFn CPUACoalitionUsageFunction = NULL;
static pthread_once_t CPUACoalitionUsageOnce = PTHREAD_ONCE_INIT;

static void CPUALoadCoalitionUsageFunction(void) {
    CPUACoalitionUsageFunction = (CPUACoalitionInfoResourceUsageFn)dlsym(
        RTLD_DEFAULT,
        "coalition_info_resource_usage"
    );
}

bool CPUACopyProcessCoalitionID(pid_t pid, uint64_t *output) {
    if (pid <= 0 || output == NULL) return false;
    CPUAProcPIDCoalitionInfo info = {0};
    int size = proc_pidinfo(
        pid,
        CPUA_PROC_PIDCOALITIONINFO,
        0,
        &info,
        sizeof(info)
    );
    if (size != sizeof(info)) return false;
    uint64_t coalition_id = info.coalition_id[CPUA_COALITION_TYPE_RESOURCE];
    if (coalition_id == 0) return false;
    *output = coalition_id;
    return true;
}

bool CPUACopyCoalitionGPUCounter(
    uint64_t coalition_id,
    CPUACoalitionGPUCounter *output
) {
    if (coalition_id == 0 || output == NULL) return false;
    pthread_once(&CPUACoalitionUsageOnce, CPUALoadCoalitionUsageFunction);
    if (CPUACoalitionUsageFunction == NULL) return false;
    CPUACoalitionResourceUsage usage = {0};
    if (CPUACoalitionUsageFunction(coalition_id, &usage, sizeof(usage)) != 0) {
        return false;
    }
    output->coalition_id = coalition_id;
    output->gpu_time = usage.gpu_time;
    return true;
}
