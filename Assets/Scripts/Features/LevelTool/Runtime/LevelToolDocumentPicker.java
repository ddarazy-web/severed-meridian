package com.serveredmeridian.leveltool;

import android.app.Activity;
import android.app.Fragment;
import android.content.Intent;
import android.net.Uri;
import android.os.Bundle;
import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.util.Arrays;

// Unity Activity를 교체하지 않고 SAF 결과만 수신하는 임시 Fragment다.
public final class LevelToolDocumentPicker extends Fragment {
    public interface Callback {
        void onResult(long id, String uri, String error, boolean write);
    }
    private static final int REQUEST = 14539;
    private static volatile LevelToolDocumentPicker active;
    private Activity ownerActivity;
    private long id;
    private boolean write, launched, finished;
    private String name;
    private Callback callback;

    public static void begin(final Activity activity, final long id, final boolean write,
                             final String name, final Callback callback) {
        activity.runOnUiThread(() -> {
            if (active != null) {
                callback.onResult(id, null, "시스템 문서 선택기가 이미 열려 있습니다.", write);
                return;
            }
            LevelToolDocumentPicker fragment = new LevelToolDocumentPicker();
            fragment.id = id;
            fragment.write = write;
            fragment.name = name;
            fragment.callback = callback;
            fragment.ownerActivity = activity;
            active = fragment;
            try {
                activity.getFragmentManager().beginTransaction().add(fragment, "LevelToolDocumentPicker")
                    .commitAllowingStateLoss();
                activity.getFragmentManager().executePendingTransactions();
            } catch (Exception error) { fragment.finish(null, error.toString()); }
        });
    }

    public static void cancel(final long id) {
        LevelToolDocumentPicker fragment = active;
        if (fragment == null) return;
        Activity activity = fragment.ownerActivity;
        if (activity == null) return;
        activity.runOnUiThread(() -> {
            if (active == fragment && fragment.id == id) {
                // 외부 선택기의 실제 복귀까지 수신자는 남기되 Unity 요청은 즉시 해제한다.
                fragment.callback = null;
            }
        });
    }

    @Override public void onCreate(Bundle state) {
        super.onCreate(state);
        setRetainInstance(true);
    }

    @Override public void onStart() {
        super.onStart();
        ownerActivity = getActivity();
        if (launched || finished) return;
        if (callback == null) { finish(null, null); return; }
        launched = true;
        try {
            Intent intent = new Intent(write ? Intent.ACTION_CREATE_DOCUMENT : Intent.ACTION_OPEN_DOCUMENT);
            intent.addCategory(Intent.CATEGORY_OPENABLE);
            intent.setType("application/zip");
            intent.addFlags(write ? Intent.FLAG_GRANT_WRITE_URI_PERMISSION : Intent.FLAG_GRANT_READ_URI_PERMISSION);
            if (write) intent.putExtra(Intent.EXTRA_TITLE, name);
            else intent.putExtra(Intent.EXTRA_MIME_TYPES, new String[] { "application/zip", "application/x-zip-compressed", "application/octet-stream" });
            startActivityForResult(intent, REQUEST);
        } catch (Exception error) { finish(null, error.toString()); }
    }

    @Override public void onActivityResult(int request, int result, Intent data) {
        super.onActivityResult(request, result, data);
        if (request != REQUEST || finished) return;
        if (result == Activity.RESULT_CANCELED) { finish(null, null); return; }
        if (result != Activity.RESULT_OK || data == null || data.getData() == null) {
            finish(null, "문서 제공자가 올바른 선택 결과를 반환하지 않았습니다.");
            return;
        }
        finish(data.getData().toString(), null);
    }

    private void finish(String uri, String error) {
        if (finished) return;
        finished = true;
        if (active == this) active = null;
        Callback result = callback;
        callback = null;
        if (isAdded()) getFragmentManager().beginTransaction().remove(this).commitAllowingStateLoss();
        if (result != null) result.onResult(id, uri, error, write);
    }

    @Override public void onDestroy() {
        if (!finished) {
            finished = true;
            if (active == this) active = null;
            Callback result = callback;
            callback = null;
            if (result != null) result.onResult(id, null, "문서 선택기의 Activity가 종료되었습니다.", write);
        }
        ownerActivity = null;
        super.onDestroy();
    }

    public static ContentStream open(Activity activity, String uri, boolean write) throws IOException {
        Uri document = Uri.parse(uri);
        if (!"content".equals(document.getScheme())) throw new IOException("SAF content URI가 아닙니다.");
        if (write) {
            OutputStream stream = activity.getContentResolver().openOutputStream(document, "wt");
            if (stream == null) throw new IOException("문서 쓰기 스트림이 없습니다.");
            return new ContentStream(null, stream);
        }
        InputStream stream = activity.getContentResolver().openInputStream(document);
        if (stream == null) throw new IOException("문서 읽기 스트림이 없습니다.");
        return new ContentStream(stream, null);
    }

    public static final class ContentStream {
        private InputStream input;
        private OutputStream output;
        ContentStream(InputStream input, OutputStream output) { this.input = input; this.output = output; }
        public synchronized byte[] read(int count) throws IOException {
            if (input == null) throw new IOException("읽기 스트림이 닫혔습니다.");
            byte[] bytes = new byte[count];
            int read = input.read(bytes);
            return read < 0 ? null : read == count ? bytes : Arrays.copyOf(bytes, read);
        }
        public synchronized void write(byte[] bytes) throws IOException {
            if (output == null) throw new IOException("쓰기 스트림이 닫혔습니다.");
            output.write(bytes);
        }
        public synchronized void flush() throws IOException { if (output != null) output.flush(); }
        public synchronized void close() throws IOException {
            InputStream oldInput = input;
            OutputStream oldOutput = output;
            input = null;
            output = null;
            if (oldInput != null) oldInput.close();
            if (oldOutput != null) oldOutput.close();
        }
    }
}
