import AppKit
import SwiftUI

struct RankedProcessList: View {
    @Bindable var model: MonitorModel
    @State private var gracefulTarget: ProcessMetric?
    @State private var forceTarget: ProcessMetric?
    @State private var showGracefulConfirmation = false
    @State private var showForceConfirmation = false
    @State private var terminationFeedback: String?

    var body: some View {
        LazyVStack(spacing: 4) {
            if model.selectedResource == .cpu {
                cpuRows
            } else {
                gpuRows
            }
            if let terminationFeedback {
                Text(terminationFeedback)
                    .font(.caption2)
                    .foregroundStyle(.secondary)
                    .frame(maxWidth: .infinity, alignment: .leading)
            }
        }
        .confirmationDialog(
            String(localized: "termination.graceful.title"),
            isPresented: $showGracefulConfirmation,
            titleVisibility: .visible
        ) {
            Button("action.terminate", role: .destructive) {
                performGracefulTermination()
            }
            Button("action.cancel", role: .cancel) {}
        } message: {
            Text(String(
                format: String(localized: "termination.graceful.message"),
                locale: .current,
                gracefulTarget?.name ?? ""
            ))
        }
        .confirmationDialog(
            String(localized: "termination.force.title"),
            isPresented: $showForceConfirmation,
            titleVisibility: .visible
        ) {
            Button("action.forceTerminate", role: .destructive) {
                performForceTermination()
            }
            Button("action.cancel", role: .cancel) {}
        } message: {
            Text(String(
                format: String(localized: "termination.force.message"),
                locale: .current,
                forceTarget?.name ?? ""
            ))
        }
    }

    @ViewBuilder
    private var cpuRows: some View {
        let rows = model.snapshot.processes.prefix(model.showTenRows ? 10 : 5)
        if rows.isEmpty {
            emptyState("panel.process.collecting")
        } else {
            ForEach(rows) { process in
                HStack(spacing: 6) {
                    Button {
                        model.expandedProcess = model.expandedProcess == process.identity
                            ? nil
                            : process.identity
                    } label: {
                        HStack(spacing: 8) {
                        processIcon(process)
                        VStack(alignment: .leading, spacing: 1) {
                            Text(process.name)
                                .lineLimit(1)
                            Text(String(
                                format: String(localized: "panel.process.pid.format"),
                                locale: .current,
                                process.identity.pid
                            ))
                                .font(.caption2)
                                .foregroundStyle(.secondary)
                        }
                        Spacer(minLength: 8)
                        Text(process.cpuUsage, format: .percent.precision(.fractionLength(1)))
                            .monospacedDigit()
                        }
                        .contentShape(Rectangle())
                    }
                    .buttonStyle(.plain)
                    .accessibilityHint(Text("panel.process.expandHint"))

                    terminationMenu(for: process)
                }

                if model.expandedProcess == process.identity {
                    ForEach(model.snapshot.expandedThreads) { thread in
                        HStack(spacing: 8) {
                            Image(systemName: "point.3.connected.trianglepath.dotted")
                                .frame(width: 24)
                                .foregroundStyle(.secondary)
                            Text(thread.name ?? String(
                                format: String(localized: "panel.thread.format"),
                                locale: .current,
                                thread.id
                            ))
                                .font(.caption)
                                .lineLimit(1)
                            Spacer()
                            Text(thread.cpuUsage, format: .percent.precision(.fractionLength(1)))
                                .font(.caption.monospacedDigit())
                        }
                        .padding(.leading, 12)
                    }
                }
            }
        }
    }

    @ViewBuilder
    private var gpuRows: some View {
        let rows = model.snapshot.gpuGroups.prefix(model.showTenRows ? 10 : 5)
        if rows.isEmpty {
            emptyState("panel.gpu.attributionUnavailable")
        } else {
            ForEach(rows) { group in
                HStack(spacing: 6) {
                    HStack(spacing: 8) {
                        Image(systemName: "square.stack.3d.up.fill")
                            .frame(width: 24)
                            .foregroundStyle(.secondary)
                        VStack(alignment: .leading, spacing: 1) {
                            Text(group.name)
                                .lineLimit(1)
                            Text(String(
                                format: String(localized: "panel.gpu.members.format"),
                                locale: .current,
                                group.members.count
                            ))
                                .font(.caption2)
                                .foregroundStyle(.secondary)
                        }
                        Spacer(minLength: 8)
                        Text(group.activityShare, format: .percent.precision(.fractionLength(1)))
                            .monospacedDigit()
                    }
                    .accessibilityElement(children: .ignore)
                    .accessibilityLabel(
                        Text(String(
                            format: String(localized: "panel.gpu.accessibility.format"),
                            locale: .current,
                            group.name,
                            Int((group.activityShare * 100).rounded())
                        ))
                    )
                    gpuTerminationMenu(for: group)
                }
            }
        }
    }

    private func processIcon(_ process: ProcessMetric) -> some View {
        Group {
            if let icon = NSRunningApplication(
                processIdentifier: process.identity.pid
            )?.icon {
                Image(nsImage: icon)
                    .resizable()
            } else {
                Image(systemName: "app.dashed")
                    .resizable()
                    .foregroundStyle(.secondary)
            }
        }
        .scaledToFit()
        .frame(width: 24, height: 24)
    }

    private func emptyState(_ key: LocalizedStringKey) -> some View {
        Text(key)
            .font(.caption)
            .foregroundStyle(.secondary)
            .frame(maxWidth: .infinity, minHeight: 72)
    }

    private func terminationMenu(for process: ProcessMetric) -> some View {
        Menu {
            Button("action.terminate", role: .destructive) {
                askToTerminate(process)
            }
        } label: {
            Image(systemName: "ellipsis.circle")
                .accessibilityLabel(Text("action.processActions"))
        }
        .menuStyle(.borderlessButton)
        .fixedSize()
    }

    @ViewBuilder
    private func gpuTerminationMenu(for group: GPUGroupMetric) -> some View {
        Menu {
            if let leader = group.leader, let process = model.processMetric(for: leader) {
                Button("action.terminate", role: .destructive) {
                    askToTerminate(process)
                }
            } else {
                ForEach(group.members, id: \.self) { member in
                    if let process = model.processMetric(for: member) {
                        Button(String(
                            format: String(localized: "action.terminate.pid.format"),
                            locale: .current,
                            process.identity.pid
                        ), role: .destructive) {
                            askToTerminate(process)
                        }
                    }
                }
            }
        } label: {
            Image(systemName: "ellipsis.circle")
                .accessibilityLabel(Text("action.processActions"))
        }
        .menuStyle(.borderlessButton)
        .fixedSize()
    }

    private func askToTerminate(_ process: ProcessMetric) {
        gracefulTarget = process
        terminationFeedback = nil
        showGracefulConfirmation = true
    }

    private func performGracefulTermination() {
        guard let target = gracefulTarget else { return }
        Task {
            let result = await model.requestGracefulTermination(target)
            if result == .forceAvailable {
                forceTarget = target
                showForceConfirmation = true
            } else {
                terminationFeedback = feedback(for: result)
            }
        }
    }

    private func performForceTermination() {
        guard let target = forceTarget else { return }
        Task {
            terminationFeedback = feedback(
                for: await model.requestForceTermination(target)
            )
        }
    }

    private func feedback(for result: TerminationResult) -> String {
        switch result {
        case .terminated: String(localized: "termination.result.terminated")
        case .forceAvailable: String(localized: "termination.result.forceAvailable")
        case .identityChanged: String(localized: "termination.result.identityChanged")
        case .forceNotAvailable: String(localized: "termination.result.forceNotAvailable")
        case .protectedTarget: String(localized: "termination.result.protected")
        case .notFound: String(localized: "termination.result.notFound")
        case .failed(let errorCode): String(
            format: String(localized: "termination.result.failed.format"),
            locale: .current,
            errorCode
        )
        }
    }
}
