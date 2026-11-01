import java.lang.instrument.ClassFileTransformer;
import java.lang.instrument.Instrumentation;
import java.security.ProtectionDomain;
import jdk.internal.org.objectweb.asm.ClassReader;
import jdk.internal.org.objectweb.asm.ClassVisitor;
import jdk.internal.org.objectweb.asm.ClassWriter;
import jdk.internal.org.objectweb.asm.Label;
import jdk.internal.org.objectweb.asm.MethodVisitor;
import jdk.internal.org.objectweb.asm.Opcodes;

/**
 * macOS 27 / Java 8 / LWJGL 2 compatibility.
 * Cocoa event processing may clear the current native OpenGL context. LWJGL's
 * Java-side current-context cache then remains stale. Check native isCurrent()
 * and restore the existing context before Minecraft resumes OpenGL operations.
 * Only the public Display event/update methods are transformed.
 */
public final class PCLLegacyContextAgent {
    public static void premain(String arguments, Instrumentation instrumentation) {
        instrumentation.addTransformer(new ClassFileTransformer() {
            public byte[] transform(ClassLoader loader, String name, Class<?> type,
                                    ProtectionDomain domain, byte[] bytes) {
                if (!"org/lwjgl/opengl/Display".equals(name)) return null;
                try {
                    ClassReader reader = new ClassReader(bytes);
                    final int[] patched = {0};
                    ClassWriter writer = new ClassWriter(reader,
                            ClassWriter.COMPUTE_MAXS | ClassWriter.COMPUTE_FRAMES);
                    reader.accept(new ClassVisitor(Opcodes.ASM5, writer) {
                        public MethodVisitor visitMethod(int access, String method, String descriptor,
                                                         String signature, String[] exceptions) {
                            MethodVisitor original = super.visitMethod(access, method, descriptor, signature, exceptions);
                            boolean target = ("processMessages".equals(method) && "()V".equals(descriptor))
                                    || ("update".equals(method) && ("()V".equals(descriptor) || "(Z)V".equals(descriptor)));
                            if (!target || (access & Opcodes.ACC_STATIC) == 0) return original;
                            patched[0]++;
                            return new MethodVisitor(Opcodes.ASM5, original) {
                                public void visitInsn(int opcode) {
                                    if (opcode == Opcodes.RETURN) {
                                        Label skip = new Label();
                                        super.visitMethodInsn(Opcodes.INVOKESTATIC, "org/lwjgl/opengl/Display", "isCreated", "()Z", false);
                                        super.visitJumpInsn(Opcodes.IFEQ, skip);
                                        super.visitMethodInsn(Opcodes.INVOKESTATIC, "org/lwjgl/opengl/Display", "isCurrent", "()Z", false);
                                        super.visitJumpInsn(Opcodes.IFNE, skip);
                                        super.visitMethodInsn(Opcodes.INVOKESTATIC, "org/lwjgl/opengl/Display", "makeCurrent", "()V", false);
                                        super.visitLabel(skip);
                                    }
                                    super.visitInsn(opcode);
                                }
                            };
                        }
                    }, ClassReader.EXPAND_FRAMES);
                    if (patched[0] != 3) throw new IllegalStateException("Unexpected LWJGL 2 Display API");
                    System.err.println("[PCL Legacy GL] macOS OpenGL context compatibility enabled; patched methods=" + patched[0]);
                    return writer.toByteArray();
                } catch (Throwable failure) {
                    failure.printStackTrace();
                    throw new IllegalStateException("LWJGL 2 context transformer failed", failure);
                }
            }
        });
    }
}
